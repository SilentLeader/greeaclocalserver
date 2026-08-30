using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GreeACLocalServer.UI.Services;

/// <summary>
/// Builds the Wi-Fi provisioning command that the user copies into their terminal.
/// The command sends a JSON payload over UDP to the GREE device while it is in AP mode.
/// All string handling is done structurally: the payload is serialized with
/// <see cref="JsonSerializer"/> and then quoted according to the rules of the target shell,
/// so arbitrary SSID/password values (including shell metacharacters) cannot break the
/// command or inject code when pasted.
/// </summary>
public static class WifiCommandBuilder
{
    /// <summary>Default gateway address of a GREE device running in AP mode.</summary>
    public const string DeviceApGateway = "192.168.1.1";

    /// <summary>UDP port the device listens on for provisioning payloads.</summary>
    public const int DeviceApPort = 7000;

    /// <summary>
    /// Control characters (CR, LF, NUL, ...) break the command in every shell and cannot
    /// appear in a real SSID/password, so they are rejected before a command is generated.
    /// </summary>
    public static bool ContainsControlCharacters(string? value) =>
        value is not null && value.Any(char.IsControl);

    /// <summary>Serializes the provisioning payload as valid, correctly escaped JSON.</summary>
    public static string BuildJsonPayload(string ssid, string password) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["psw"] = password,
            ["ssid"] = ssid,
            ["t"] = "wlan",
        });

    /// <summary>
    /// Wraps <paramref name="value"/> in POSIX single quotes, escaping embedded single
    /// quotes as <c>'\''</c>. The result is a single, literal shell word.
    /// </summary>
    public static string ShellQuotePosix(string value) =>
        "'" + value.Replace("'", "'\\''") + "'";

    /// <summary>
    /// Wraps <paramref name="value"/> in PowerShell single quotes, doubling embedded
    /// single quotes. The result is a single, literal PowerShell string.
    /// </summary>
    public static string ShellQuotePowerShell(string value) =>
        "'" + value.Replace("'", "''") + "'";

    /// <summary>
    /// Builds the copy-paste command for the given OS/shell selection.
    /// </summary>
    public static string Build(string os, string ssid, string password)
    {
        var json = BuildJsonPayload(ssid, password);

        return os switch
        {
            "windows-powershell" => BuildPowerShellCommand(json),
            "windows-ncat" => BuildNetcatCommand(json, "ncat"),
            _ => BuildNetcatCommand(json, "nc"),
        };
    }

    private static string BuildNetcatCommand(string json, string tool) =>
        $"printf %s {ShellQuotePosix(json)} | {tool} -u {DeviceApGateway} {DeviceApPort}";

    private static string BuildPowerShellCommand(string json) =>
        $"$bytes = [System.Text.Encoding]::UTF8.GetBytes({ShellQuotePowerShell(json)}); " +
        "$client = New-Object System.Net.Sockets.UdpClient; " +
        $"$client.Connect('{DeviceApGateway}', {DeviceApPort}); " +
        "$client.Send($bytes, $bytes.Length); $client.Close()";
}
