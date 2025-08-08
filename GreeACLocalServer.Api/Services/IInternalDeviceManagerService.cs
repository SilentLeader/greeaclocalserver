using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.Api.Services;

public interface IInternalDeviceManagerService: IDeviceManagerService
{
    void UpdateOrAdd(string macAddress, string ipAddress);
    void RemoveStaleDevices();
}
