using GreeACLocalServer.Shared.ValueObjects;

namespace GreeACLocalServer.Shared.Contracts;

/// <summary>
/// The last successfully polled operating state of an air conditioner. Read-only:
/// the server never changes these. Present on <see cref="DeviceDto.RuntimeState"/>
/// only while the most recent poll succeeded — a failed poll clears it back to null.
/// </summary>
/// <param name="CurrentTemperature">
/// Indoor sensor reading in whole degrees Celsius, or null when the device has no
/// sensor / reported an implausible value. Always Celsius, regardless of
/// <paramref name="TemperatureUnit"/>.
/// </param>
public record AcRuntimeStateDto(
    bool Power,
    AcMode Mode,
    int TargetTemperature,
    AcTemperatureUnit TemperatureUnit,
    DateTime QueriedUtc,
    int? CurrentTemperature = null);
