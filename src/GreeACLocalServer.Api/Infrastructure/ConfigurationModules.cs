namespace GreeACLocalServer.Api.Infrastructure;

internal static class ConfigurationModules
{
    public static IConfigurationBuilder BuildConfiguration(this IConfigurationBuilder configBuilder)
    {
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        if (OperatingSystem.IsLinux())
        {
            configBuilder.AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true);
        }

        configBuilder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

        return configBuilder;
    }
}