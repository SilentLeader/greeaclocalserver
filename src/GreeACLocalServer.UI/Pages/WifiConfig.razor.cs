using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.UI.Pages;

public partial class WifiConfig : ComponentBase
{
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IBrowserDetectionService BrowserDetection { get; set; } = default!;

    private string _wifiSsid = string.Empty;
    private string _wifiPassword = string.Empty;
    private string _selectedOs = "linux";
    private bool _showPassword = false;
    private string _osDetectionMessage = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await DetectOperatingSystem();
    }

    private async Task DetectOperatingSystem()
    {
        try
        {
            var detectedOs = await BrowserDetection.DetectOperatingSystemAsync();
            _selectedOs = detectedOs;

            // Set appropriate message based on detected OS
            _osDetectionMessage = detectedOs switch
            {
                "windows-powershell" => "Auto-detected: Windows (PowerShell selected - no additional software needed)",
                "macos" => "Auto-detected: macOS",
                "linux" => "Auto-detected: Linux",
                _ => "Could not detect OS - defaulted to Linux"
            };
        }
        catch (Exception)
        {
            _selectedOs = "linux";
            _osDetectionMessage = "OS detection unavailable - defaulted to Linux";
        }
    }

    private bool IsFormValid =>
        !string.IsNullOrWhiteSpace(_wifiSsid) &&
        !string.IsNullOrWhiteSpace(_wifiPassword);

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
    }

    private string GetGeneratedCommand()
    {
        if (!IsFormValid)
        {
            return string.Empty;
        }

        var escapedPassword = EscapeJsonString(_wifiPassword);
        var escapedSsid = EscapeJsonString(_wifiSsid);
        var jsonPayload = $"{{\\\"psw\\\": \"{escapedPassword}\",\\\"ssid\\\": \"{escapedSsid}\",\\\"t\\\": \"wlan\"}}";

        return _selectedOs switch
        {
            "linux" => $"echo -n \"{jsonPayload}\" | nc -cu 192.168.1.1 7000",
            "macos" => $"echo -n \"{jsonPayload}\" | nc -cu 192.168.1.1 7000",
            "windows-wsl" => $"echo -n \"{jsonPayload}\" | nc -cu 192.168.1.1 7000",
            "windows-powershell" => GetPowerShellCommand(jsonPayload),
            "windows-ncat" => $"echo {jsonPayload} | ncat -u 192.168.1.1 7000",
            _ => $"echo -n \"{jsonPayload}\" | nc -cu 192.168.1.1 7000"
        };
    }

    private string GetPowerShellCommand(string jsonPayload)
    {
        // PowerShell UDP command using .NET classes - remove extra escaping for PowerShell
        var cleanPayload = jsonPayload.Replace("\\\"", "\"");
        return $"$bytes = [System.Text.Encoding]::UTF8.GetBytes('{cleanPayload}'); $client = New-Object System.Net.Sockets.UdpClient; $client.Connect('192.168.1.1', 7000); $client.Send($bytes, $bytes.Length); $client.Close()";
    }

    private string EscapeJsonString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private async Task CopyToClipboard()
    {
        if (!IsFormValid)
        {
            return;
        }

        try
        {
            var command = GetGeneratedCommand();
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", command);
            Snackbar.Add("Command copied to clipboard!", Severity.Success);
        }
        catch (Exception)
        {
            Snackbar.Add("Failed to copy to clipboard. Please copy manually.", Severity.Error);
        }
    }

    private void ClearForm()
    {
        _wifiSsid = string.Empty;
        _wifiPassword = string.Empty;
        // Don't reset OS selection to preserve auto-detection
        _showPassword = false;
        Snackbar.Add("Form cleared", Severity.Info);
    }
}
