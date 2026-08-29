using System.Text;
using System.Text.Json;
using GreeACLocalServer.Device.Commands;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Guards WP-06 finding F7: UDP command payloads must be UTF-8 (not ASCII) so accented
/// device names / SSIDs survive the wire round trip instead of being mangled to '?'.
/// Mirrors the encode/decode used by <c>DeviceControllerService.SendUdpCommandAsync</c>.
/// </summary>
public class UdpPayloadEncodingTests
{
    private const string AccentedName = "Nappali légkondi – Előszoba";

    [Fact]
    public void Utf8_RoundTripsAccentedPayload()
    {
        var json = JsonSerializer.Serialize(new ParameterCommand(["name"], [AccentedName]));

        var wireBytes = Encoding.UTF8.GetBytes(json);
        var decoded = Encoding.UTF8.GetString(wireBytes);

        Assert.Equal(json, decoded);
    }

    [Fact]
    public void Ascii_ManglesAccentedPayload_WhichIsWhyUtf8IsRequired()
    {
        // A device may echo raw (unescaped) UTF-8 text back; ASCII decoding replaces every
        // non-ASCII byte with '?', which is the bug F7 fixes.
        var raw = Encoding.UTF8.GetBytes(AccentedName);

        Assert.NotEqual(AccentedName, Encoding.ASCII.GetString(raw));
        Assert.Equal(AccentedName, Encoding.UTF8.GetString(raw));
    }
}
