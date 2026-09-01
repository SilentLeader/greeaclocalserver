using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.ValueObjects;
using MudBlazor;

namespace GreeACLocalServer.UI.Helpers;

/// <summary>
/// UI formatting for the read-only AC operating state (<see cref="AcRuntimeStateDto"/>).
/// </summary>
public static class AcStateHelpers
{
    /// <summary>Material icon for an operating mode.</summary>
    public static string ModeIcon(AcMode mode) => mode switch
    {
        AcMode.Auto => Icons.Material.Filled.Loop,
        AcMode.Cool => Icons.Material.Filled.AcUnit,
        AcMode.Dry => Icons.Material.Filled.WaterDrop,
        AcMode.Fan => Icons.Material.Filled.Air,
        AcMode.Heat => Icons.Material.Filled.LocalFireDepartment,
        _ => Icons.Material.Filled.HelpOutline
    };

    /// <summary>Human-readable mode name.</summary>
    public static string ModeLabel(AcMode mode) => mode switch
    {
        AcMode.Auto => "Auto",
        AcMode.Cool => "Cool",
        AcMode.Dry => "Dry",
        AcMode.Fan => "Fan only",
        AcMode.Heat => "Heat",
        _ => "Unknown"
    };

    public static string PowerIcon(bool power)
        => power ? Icons.Material.Filled.PowerSettingsNew : Icons.Material.Filled.PowerOff;

    public static Color PowerColor(bool power) => power ? Color.Success : Color.Default;

    public static string PowerLabel(bool power) => power ? "On" : "Off";

    /// <summary>Formats the setpoint, e.g. <c>24°C</c> / <c>72°F</c>.</summary>
    public static string FormatSetpoint(AcRuntimeStateDto state)
    {
        var unit = state.TemperatureUnit == AcTemperatureUnit.Fahrenheit ? "F" : "C";
        return $"{state.TargetTemperature}°{unit}";
    }

    /// <summary>Measured indoor temperature, e.g. <c>26°C</c>; empty when unavailable.</summary>
    public static string FormatCurrent(AcRuntimeStateDto state)
        => state.CurrentTemperature is { } c ? $"{c}°C" : string.Empty;

    /// <summary>
    /// Tooltip text for the mode icon. Without a sensor reading: "Cool · 24°C".
    /// With one: "Cool · set 24°C · now 26°C".
    /// </summary>
    public static string ModeTooltip(AcRuntimeStateDto state)
        => state.CurrentTemperature is { } c
            ? $"{ModeLabel(state.Mode)} · set {FormatSetpoint(state)} · now {c}°C"
            : $"{ModeLabel(state.Mode)} · {FormatSetpoint(state)}";
}
