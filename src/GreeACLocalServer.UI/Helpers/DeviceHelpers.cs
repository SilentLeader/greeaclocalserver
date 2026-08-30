using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.UI.Helpers;

/// <summary>
/// Helper methods for device-related formatting and utilities
/// </summary>
public static class DeviceHelpers
{
    /// <summary>
    /// Determines whether a device is considered online: it must have contacted
    /// the server within the configured timeout window. This is the single place
    /// the "online" threshold is defined on the UI side; the window comes from
    /// the server's DeviceManager:DeviceTimeoutMinutes setting.
    /// </summary>
    /// <param name="device">The device to evaluate.</param>
    /// <param name="deviceTimeoutMinutes">
    /// The timeout window in minutes (from <see cref="Shared.DTOs.ServerConfigResponse.DeviceTimeoutMinutes"/>).
    /// </param>
    public static bool IsDeviceOnline(DeviceDto device, int deviceTimeoutMinutes)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-deviceTimeoutMinutes);
        return device.LastConnectionTimeUtc > threshold;
    }

    /// <summary>
    /// Formats a MAC address with colons (e.g., "000000000000" -> "00:00:00:00:00:00")
    /// </summary>
    /// <param name="macAddress">The MAC address string to format</param>
    /// <returns>Formatted MAC address with colons, or original string if not in expected format</returns>
    public static string FormatMacAddress(string macAddress)
    {
        if (string.IsNullOrEmpty(macAddress) || macAddress.Length != 12)
        {
            return macAddress; // Return as-is if not in expected format
        }

        return string.Join(":", Enumerable.Range(0, 6)
            .Select(i => macAddress.Substring(i * 2, 2)));
    }
}
