namespace GreeACLocalServer.Api.Interfaces;

/// <summary>Result of a firmware "is there a newer release" lookup.</summary>
public record FirmwareUpdateInfo(string LatestVersion, bool UpdateAvailable, bool ForcedUpgrade);

public interface IFirmwareUpdateService
{
    /// <summary>
    /// Returns the latest published firmware for <paramref name="firmwareCode"/>,
    /// or <c>null</c> when the check is disabled, the code is unknown, or the
    /// lookup fails. Results are cached per firmware code.
    /// </summary>
    /// <param name="allowRemoteFetch">
    /// When <c>false</c>, only an existing cache entry is used (even a stale one)
    /// and no HTTP request is ever made; a full cache miss returns <c>null</c>.
    /// Callers on latency-sensitive paths (device list, SignalR push) pass
    /// <c>false</c>.
    /// </param>
    Task<FirmwareUpdateInfo?> CheckAsync(string firmwareCode, string currentVersion, bool allowRemoteFetch = true, CancellationToken cancellationToken = default);
}
