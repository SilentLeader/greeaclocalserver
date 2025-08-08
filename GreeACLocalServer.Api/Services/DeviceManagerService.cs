using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GreeACLocalServer.Api.Models;
using GreeACLocalServer.Api.Options;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.Api.Services;

public class DeviceManagerService(IOptions<DeviceManagerOptions> options) : IInternalDeviceManagerService
{
    private readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();
    private readonly DeviceManagerOptions _options = options.Value;

    public void UpdateOrAdd(string macAddress, string ipAddress)
    {
        _deviceStates.AddOrUpdate(macAddress,
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
    }

    public void RemoveStaleDevices()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_options.DeviceTimeoutMinutes);
        foreach (var kvp in _deviceStates)
        {
            if (kvp.Value.LastConnectionTime < threshold)
                _deviceStates.TryRemove(kvp.Key, out _);
        }
    }

    public IEnumerable<DeviceDto> GetAllDeviceStates()
    {
        RemoveStaleDevices();
        return _deviceStates.Values.Select(v => new DeviceDto(v.MacAddress, v.IpAddress, v.LastConnectionTime));
    }

    public DeviceDto? Get(string macAddress)
    {
        RemoveStaleDevices();
        if (_deviceStates.TryGetValue(macAddress, out var state))
        {
            return new DeviceDto(state.MacAddress, state.IpAddress, state.LastConnectionTime);
        }
        return null;
    }
}
