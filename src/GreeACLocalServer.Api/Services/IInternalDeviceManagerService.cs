using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.Api.Services;

public interface IInternalDeviceManagerService: IDeviceManagerService
{
    Task UpdateOrAddAsync(string macAddress, string ipAddress);
    void RemoveStaleDevices();
}
