using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Server-side implementation of browser detection service.
/// Returns default values since JavaScript is not available during server-side rendering.
/// </summary>
public class ServerBrowserDetectionService : IBrowserDetectionService
{
    public Task<string> DetectOperatingSystemAsync()
    {
        // Default to linux on server-side since we can't access browser APIs
        // This will be overridden when the client-side takes over
        return Task.FromResult("linux");
    }

    public Task<string> GetUserAgentAsync()
    {
        // Not available on server-side
        return Task.FromResult(string.Empty);
    }

    public Task<string> GetPlatformAsync()
    {
        // Not available on server-side
        return Task.FromResult(string.Empty);
    }
}
