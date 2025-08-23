namespace GreeACLocalServer.Api.Interfaces;

public interface IInternalDeviceManagerService: IDeviceManagerService
{
    Task UpdateOrAddAsync(string macAddress, string ipAddress);
    Task RemoveStaleDevicesAsync();
    Task<bool> RemoveDeviceAsync(string macAddress);
}
