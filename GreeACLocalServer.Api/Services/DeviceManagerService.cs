using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Models;
using GreeACLocalServer.Api.Options;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using GreeACLocalServer.Api.Hubs;

namespace GreeACLocalServer.Api.Services;

public class DeviceManagerService(IOptions<DeviceManagerOptions> options, IHubContext<DeviceHub>? hubContext) : IInternalDeviceManagerService
{
    private readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();
    private readonly DeviceManagerOptions _options = options.Value;
    private readonly IHubContext<DeviceHub>? _hub = hubContext;

    public void UpdateOrAdd(string macAddress, string ipAddress)
    {
        var state = _deviceStates.AddOrUpdate(macAddress,
            key => new AcDeviceState
            {
                MacAddress = macAddress,
                IpAddress = ipAddress,
                LastConnectionTime = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.IpAddress = ipAddress;
                existing.LastConnectionTime = DateTime.UtcNow;
                return existing;
            });

        // Broadcast upsert
        var dto = new DeviceDto(state.MacAddress, state.IpAddress, state.LastConnectionTime);
        _ = _hub?.Clients.All.SendAsync("DeviceUpserted", dto);
    }

    public void RemoveStaleDevices()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_options.DeviceTimeoutMinutes);
        var removed = new List<string>();
        foreach (var kvp in _deviceStates)
        {
            if (kvp.Value.LastConnectionTime < threshold)
            {
                if (_deviceStates.TryRemove(kvp.Key, out _))
                {
                    removed.Add(kvp.Key);
                }
            }
        }

        foreach (var mac in removed)
        {
            _ = _hub?.Clients.All.SendAsync("DeviceRemoved", mac);
        }
    }

    public Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default)
    {
        RemoveStaleDevices();
        IEnumerable<DeviceDto> result = _deviceStates.Values.Select(v => new DeviceDto(v.MacAddress, v.IpAddress, v.LastConnectionTime));
        return Task.FromResult(result);
    }

    public Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        RemoveStaleDevices();
        if (_deviceStates.TryGetValue(macAddress, out var state))
        {
            return Task.FromResult<DeviceDto?>(new DeviceDto(state.MacAddress, state.IpAddress, state.LastConnectionTime));
        }
        return Task.FromResult<DeviceDto?>(null);
    }
}
