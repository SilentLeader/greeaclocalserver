namespace GreeACLocalServer.Api.Services;

public interface IInternalDeviceManagerService: GreeACLocalServer.Shared.Interfaces.IDeviceManagerService
{
    void UpdateOrAdd(string macAddress, string ipAddress);
    void RemoveStaleDevices();
}
