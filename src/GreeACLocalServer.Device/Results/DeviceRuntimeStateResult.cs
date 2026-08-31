namespace GreeACLocalServer.Device.Results;

/// <summary>
/// Outcome of an outbound "operating state" query (GREE status columns
/// <c>Pow</c> / <c>Mod</c> / <c>SetTem</c> / <c>TemUn</c>). Values are the raw
/// integers reported by the device; the caller maps them onto its own enums.
/// </summary>
public class DeviceRuntimeStateResult(
    bool success,
    string message,
    string? errorCode = null,
    bool? power = null,
    int? mode = null,
    int? targetTemperature = null,
    int? temperatureUnit = null,
    string? macAddress = null) : ResultBase(success, message, errorCode)
{
    /// <summary>Raw <c>Pow</c>: <c>true</c> when the unit is switched on.</summary>
    public bool? Power { get; } = power;

    /// <summary>Raw <c>Mod</c>: 0=auto, 1=cool, 2=dry, 3=fan, 4=heat.</summary>
    public int? Mode { get; } = mode;

    /// <summary>Raw <c>SetTem</c>: target temperature in the unit given by <see cref="TemperatureUnit"/>.</summary>
    public int? TargetTemperature { get; } = targetTemperature;

    /// <summary>Raw <c>TemUn</c>: 0=Celsius, 1=Fahrenheit.</summary>
    public int? TemperatureUnit { get; } = temperatureUnit;

    public string? MacAddress { get; } = macAddress;
}
