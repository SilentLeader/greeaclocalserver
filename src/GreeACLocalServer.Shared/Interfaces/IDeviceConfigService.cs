using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.DTOs;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IDeviceConfigService
{
    Task<DeviceStatusResponse> QueryDeviceStatusAsync(QueryDeviceStatusRequest request, CancellationToken cancellationToken = default);
    Task<DeviceOperationResponse> SetDeviceNameAsync(UpdateDeviceNameRequest request, CancellationToken cancellationToken = default);
    Task<DeviceOperationResponse> SetRemoteHostAsync(UpdateRemoteHostRequest request, CancellationToken cancellationToken = default);
}
