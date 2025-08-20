using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.DTOs;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IDeviceConfigService
{
    Task<DeviceStatusResponse> QueryDeviceStatusAsync(DeviceStatusRequest request, CancellationToken cancellationToken = default);
    Task<DeviceOperationResponse> SetDeviceNameAsync(SetDeviceNameRequest request, CancellationToken cancellationToken = default);
    Task<DeviceOperationResponse> SetRemoteHostAsync(SetRemoteHostRequest request, CancellationToken cancellationToken = default);
}
