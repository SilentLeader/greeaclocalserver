using System.Collections.Generic;
using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IDeviceManagerService
{
    IEnumerable<DeviceDto> GetAllDeviceStates();
    DeviceDto? Get(string macAddress);
}
