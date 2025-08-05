using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GreeACLocalServer.Api.Models;
using GreeHandlerResponse = GreeACLocalServer.Api.Models.GreeHandlerResponse;

namespace GreeACLocalServer.Api.Services
{
    public class DeviceManagerService
    {
        private readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();

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

        public IEnumerable<AcDeviceState> GetAllDeviceStates() => _deviceStates.Values;
    }
}
