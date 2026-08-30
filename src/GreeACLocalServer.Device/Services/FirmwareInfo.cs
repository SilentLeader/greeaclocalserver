using System.Globalization;
using System.Text.RegularExpressions;

namespace GreeACLocalServer.Device.Services;

/// <summary>
/// Helpers for interpreting the GREE Wi-Fi module firmware identifier (the
/// <c>hid</c> status column), e.g. <c>"362001065736+U-QCOM4004CV3.76.bin"</c>:
/// the leading digits are the <em>firmware code</em> used for update lookups and
/// the trailing <c>V&lt;major&gt;.&lt;minor&gt;...</c> is the human-readable
/// version.
/// </summary>
public static partial class FirmwareInfo
{
    [GeneratedRegex(@"^(?<code>\d+)\+.*?V(?<ver>\d+(?:\.\d+)+)\.bin$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HidPattern();

    /// <summary>
    /// Parses a raw <c>hid</c> string. Returns <c>true</c> only when both the
    /// firmware code and version could be extracted; otherwise both out
    /// parameters are empty and the caller should keep the raw value.
    /// </summary>
    public static bool TryParse(string? hid, out string firmwareCode, out string version)
    {
        firmwareCode = string.Empty;
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(hid))
        {
            return false;
        }

        var match = HidPattern().Match(hid.Trim());
        if (!match.Success)
        {
            return false;
        }

        firmwareCode = match.Groups["code"].Value;
        version = match.Groups["ver"].Value;
        return true;
    }

    /// <summary>
    /// Compares two dotted numeric version strings (e.g. <c>"3.76"</c> vs
    /// <c>"3.77"</c>). Missing trailing components are treated as zero. Returns a
    /// negative value when <paramref name="left"/> is older, zero when equal, and
    /// a positive value when <paramref name="left"/> is newer. Unparseable input
    /// is treated as the lowest possible version.
    /// </summary>
    public static int CompareVersions(string? left, string? right)
    {
        var a = ParseParts(left);
        var b = ParseParts(right);
        var length = Math.Max(a.Length, b.Length);

        for (var i = 0; i < length; i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y)
            {
                return x.CompareTo(y);
            }
        }

        return 0;
    }

    private static int[] ParseParts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return [];
        }

        var raw = version.Trim().TrimStart('v', 'V').Split('.');
        var parts = new List<int>(raw.Length);
        foreach (var segment in raw)
        {
            if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                parts.Add(value);
            }
            else
            {
                break;
            }
        }

        return [.. parts];
    }
}
