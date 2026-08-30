namespace GreeACLocalServer.Device.Results;

public class DeviceFirmwareResult(
    bool success,
    string message,
    string? errorCode = null,
    string? hid = null,
    string? firmwareVersion = null,
    string? firmwareCode = null,
    string? macAddress = null) : ResultBase(success, message, errorCode)
{
    /// <summary>Raw <c>hid</c> string reported by the device, when available.</summary>
    public string? Hid { get; } = hid;

    /// <summary>Parsed dotted version (e.g. <c>3.76</c>), empty when unparseable.</summary>
    public string? FirmwareVersion { get; } = firmwareVersion;

    /// <summary>Parsed firmware code used for update lookups, empty when unparseable.</summary>
    public string? FirmwareCode { get; } = firmwareCode;

    public string? MacAddress { get; } = macAddress;
}
