namespace GreeACLocalServer.Shared.Contracts;

public record DeviceDto(
    string MacAddress,
    string IpAddress,
    string DNSName,
    DateTime LastConnectionTimeUtc,
    IReadOnlyList<DeviceEndpointDto>? Endpoints = null)
{
    /// <summary>Connection endpoints the device has been seen using, ordered by port.</summary>
    public IReadOnlyList<DeviceEndpointDto> Endpoints { get; init; } = Endpoints ?? [];

    /// <summary>Firmware version last reported by the device (e.g. <c>3.76</c>); null when unknown.</summary>
    public string? FirmwareVersion { get; init; }

    /// <summary>Firmware code used for update lookups; null when unknown.</summary>
    public string? FirmwareCode { get; init; }

    /// <summary>Latest firmware version known to the GREE update server; null when the check is disabled or pending.</summary>
    public string? LatestFirmwareVersion { get; init; }

    /// <summary>
    /// True when a newer firmware is available, false when up to date, null when
    /// the update check is disabled or has not completed yet.
    /// </summary>
    public bool? UpdateAvailable { get; init; }

    /// <summary>
    /// Last successfully polled operating state (power / mode / setpoint / unit);
    /// null when it has never been read or the most recent poll failed.
    /// </summary>
    public AcRuntimeStateDto? RuntimeState { get; init; }
}
