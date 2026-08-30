using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IDeviceManagerService
{
    Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default);
    Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default);
    Task<bool> RemoveDeviceAsync(string macAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-queries the device's firmware identifier over the local network and
    /// returns the refreshed device state. Returns <c>null</c> when the device is
    /// unknown or the query fails.
    /// </summary>
    Task<DeviceDto?> RefreshFirmwareAsync(string macAddress, CancellationToken cancellationToken = default);
}
