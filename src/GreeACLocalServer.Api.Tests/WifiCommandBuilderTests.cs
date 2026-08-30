using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GreeACLocalServer.UI.Services;

namespace GreeACLocalServer.Api.Tests;

public class WifiCommandBuilderTests
{
    private const string NastySsid = "my net'work";
    private const string NastyPassword = "p'a\"s$s`w\\ord$(rm -rf /)!";

    [Fact]
    public void BuildJsonPayload_ProducesValidRoundTrippableJson()
    {
        var json = WifiCommandBuilder.BuildJsonPayload(NastySsid, NastyPassword);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(NastyPassword, doc.RootElement.GetProperty("psw").GetString());
        Assert.Equal(NastySsid, doc.RootElement.GetProperty("ssid").GetString());
        Assert.Equal("wlan", doc.RootElement.GetProperty("t").GetString());
    }

    [Theory]
    [InlineData("linux")]
    [InlineData("macos")]
    [InlineData("windows-wsl")]
    [InlineData("windows-ncat")]
    [InlineData("something-else")]
    public void Build_PosixShells_DoNotUseNonPortableNcFlag(string os)
    {
        var command = WifiCommandBuilder.Build(os, NastySsid, NastyPassword);

        Assert.DoesNotContain(" -c", command);
        Assert.Contains($"{WifiCommandBuilder.DeviceApGateway} {WifiCommandBuilder.DeviceApPort}", command);
    }

    [Fact]
    public void ShellQuotePowerShell_DoublesSingleQuotes()
    {
        Assert.Equal("'a''b'", WifiCommandBuilder.ShellQuotePowerShell("a'b"));
    }

    [Fact]
    public void ShellQuotePosix_EscapesSingleQuotes()
    {
        Assert.Equal("'a'\\''b'", WifiCommandBuilder.ShellQuotePosix("a'b"));
    }

    [Fact]
    public void Build_PowerShell_QuotesPayloadAndKeepsItValidJson()
    {
        var command = WifiCommandBuilder.Build("windows-powershell", NastySsid, NastyPassword);

        // Extract the single-quoted PowerShell literal and undo the '' doubling.
        var start = command.IndexOf("GetBytes('", StringComparison.Ordinal) + "GetBytes('".Length;
        var end = command.IndexOf("')", start, StringComparison.Ordinal);
        var literal = command.Substring(start, end - start).Replace("''", "'");

        using var doc = JsonDocument.Parse(literal);
        Assert.Equal(NastyPassword, doc.RootElement.GetProperty("psw").GetString());
    }

    [Theory]
    [InlineData("ok", false)]
    [InlineData("line\nbreak", true)]
    [InlineData("carriage\rreturn", true)]
    [InlineData("null\0char", true)]
    public void ContainsControlCharacters_DetectsControlChars(string value, bool expected)
    {
        Assert.Equal(expected, WifiCommandBuilder.ContainsControlCharacters(value));
    }

    [Fact]
    public void Build_PosixCommand_ExecutedBySh_SendsExactJsonAndDoesNotExecuteInjectedCode()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return; // POSIX shell not available on this platform
        }

        var expectedJson = WifiCommandBuilder.BuildJsonPayload(NastySsid, NastyPassword);
        var quoted = WifiCommandBuilder.ShellQuotePosix(expectedJson);

        // Replace the netcat pipe with a harmless sink so we can observe exactly what
        // the shell hands to the network tool.
        var script = $"printf %s {quoted} | cat";

        var psi = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(script);
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        Assert.Equal(expectedJson, stdout);
    }
}
