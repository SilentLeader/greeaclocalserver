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

    /// <summary>
    /// MAC addresses the runtime-state poller should query this cycle: recently
    /// connected devices, minus those inside their post-failure backoff window and
    /// those that have failed <paramref name="maxConsecutiveFailures"/> times in a
    /// row (until they reconnect). <paramref name="maxConsecutiveFailures"/> &lt;= 0
    /// disables the "give up" filter.
    /// </summary>
    IReadOnlyCollection<string> GetRuntimeStatePollTargets(
        TimeSpan window, TimeSpan failureBackoff, int maxConsecutiveFailures);
}
