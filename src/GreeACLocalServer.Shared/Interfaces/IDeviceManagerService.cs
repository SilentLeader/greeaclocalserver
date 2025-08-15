using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IDeviceManagerService
{
    Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default);
    Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default);
}
