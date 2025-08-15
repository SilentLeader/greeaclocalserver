namespace GreeACLocalServer.UI.Services;

/// <summary>
/// Helper methods for device-related formatting and utilities
/// </summary>
public static class DeviceHelpers
{
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
