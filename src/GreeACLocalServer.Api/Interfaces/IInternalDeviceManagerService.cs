namespace GreeACLocalServer.Api.Interfaces;

public interface IInternalDeviceManagerService : IDeviceManagerService
{
    Task UpdateOrAddAsync(string macAddress, string? ipAddress, int port = 0, bool isTls = false);
    Task<bool> RemoveDeviceAsync(string macAddress);
}
