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
    Task<FirmwareUpdateInfo?> CheckAsync(string firmwareCode, string currentVersion, CancellationToken cancellationToken = default);
}
