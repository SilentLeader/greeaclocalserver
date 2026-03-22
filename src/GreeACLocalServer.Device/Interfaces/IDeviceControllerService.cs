using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Results;

namespace GreeACLocalServer.Device.Interfaces;

public interface IDeviceControllerService
{
    Task<DeviceStatusResult> GetDeviceStatusAsync(GetDeviceStatusRequest request, CancellationToken cancellationToken = default);
    Task<SimpleDeviceOperationResult> SetDeviceNameAsync(SetDeviceNameRequest request, CancellationToken cancellationToken = default);
    Task<SimpleDeviceOperationResult> SetRemoteHostAsync(SetRemoteHostRequest request, CancellationToken cancellationToken = default);
}