namespace GreeACLocalServer.Api.Interfaces;

public interface IInternalDeviceManagerService : IDeviceManagerService
{
    Task UpdateOrAddAsync(string macAddress, string? ipAddress, int port = 0, bool isTls = false);
    Task<bool> RemoveDeviceAsync(string macAddress);

    /// <summary>
    /// MAC addresses of devices whose last connection was within
    /// <paramref name="window"/>. Used by the runtime-state poller.
    /// </summary>
    IReadOnlyCollection<string> GetRecentlyConnectedMacs(TimeSpan window);
}
