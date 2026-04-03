namespace GreeACLocalServer.Api.Infrastructure;

internal static class HostingModules
{
    public static IHostBuilder ConfigureHostingServices(this IHostBuilder hostBuilder)
    {
        if (OperatingSystem.IsLinux())
        {
            hostBuilder.UseSystemd();
        }
        else if (OperatingSystem.IsWindows())
        {
            hostBuilder.UseWindowsService();
        }

        return hostBuilder;
    }
}