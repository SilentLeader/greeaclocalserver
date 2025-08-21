using Microsoft.JSInterop;
using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.UI.Services;

/// <summary>
/// Client-side implementation of browser detection service using JavaScript interop.
/// </summary>
public class ClientBrowserDetectionService(IJSRuntime jsRuntime) : IBrowserDetectionService
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async Task<string> DetectOperatingSystemAsync()
    {
        try
        {
            var userAgent = await GetUserAgentAsync();
            var platform = await GetPlatformAsync();
            
            // Detect operating system based on user agent and platform
            if (IsWindows(userAgent, platform))
            {
                return "windows-powershell"; // Default to PowerShell (no additional software needed)
            }
            else if (IsMac(userAgent, platform))
            {
                return "macos";
            }
            else if (IsLinux(userAgent, platform))
            {
                return "linux";
            }
            else
            {
                return "linux"; // Default fallback
            }
        }
        catch (Exception)
        {
            // Fallback if detection fails
            return "linux";
        }
    }

    public async Task<string> GetUserAgentAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task<string> GetPlatformAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("eval", "navigator.platform");
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static bool IsWindows(string userAgent, string platform)
    {
        return userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) || 
               platform.Contains("Win", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMac(string userAgent, string platform)
    {
        return userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase) || 
               platform.Contains("Mac", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLinux(string userAgent, string platform)
    {
        return userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) || 
               platform.Contains("Linux", StringComparison.OrdinalIgnoreCase);
    }
}
