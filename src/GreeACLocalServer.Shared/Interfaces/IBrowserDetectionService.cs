namespace GreeACLocalServer.Shared.Interfaces;

public interface IBrowserDetectionService
{
    Task<string> DetectOperatingSystemAsync();
    Task<string> GetUserAgentAsync();
    Task<string> GetPlatformAsync();
}
