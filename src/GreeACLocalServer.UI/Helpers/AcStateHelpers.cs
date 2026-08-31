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

    /// <summary>Tooltip text for the mode icon: "Cool · 24°C".</summary>
    public static string ModeTooltip(AcRuntimeStateDto state)
        => $"{ModeLabel(state.Mode)} · {FormatSetpoint(state)}";
}
