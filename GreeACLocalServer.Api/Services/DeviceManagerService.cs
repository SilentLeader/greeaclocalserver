using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GreeACLocalServer.Api.Models;
using GreeACLocalServer.Api.Options;
using Microsoft.Extensions.Options;
using GreeHandlerResponse = GreeACLocalServer.Api.Models.GreeHandlerResponse;

namespace GreeACLocalServer.Api.Services;

public class DeviceManagerService(IOptions<DeviceManagerOptions> options)
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

    public IEnumerable<AcDeviceState> GetAllDeviceStates()
    {
        RemoveStaleDevices();
        return _deviceStates.Values;
    }
}
