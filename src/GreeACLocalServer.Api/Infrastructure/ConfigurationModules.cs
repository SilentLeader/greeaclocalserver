namespace GreeACLocalServer.Api.Infrastructure;

internal static class ConfigurationModules
{
    private const string LinuxConfigDirectory = "/etc/greeac-localserver";

    /// <summary>
    /// Builds the full configuration pipeline from scratch. Use this for the early
    /// bootstrap builder in <see cref="Program"/>, which has no host builder to lean on.
    /// Precedence (low to high): <c>appsettings.json</c> →
    /// <c>appsettings.{Environment}.json</c> → Linux <c>/etc</c> overrides →
    /// <c>appsettings.dev.json</c> → environment variables.
    /// </summary>
    public static IConfigurationBuilder BuildConfiguration(this IConfigurationBuilder configBuilder, string? environmentName)
    {
        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            configBuilder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
        }

        return configBuilder.AddProjectConfiguration(environmentName);
    }

    /// <summary>
    /// Adds only the project-specific configuration sources that the generic host and
    /// the web host do not add on their own: the Linux <c>/etc</c> overrides and the
    /// local <c>appsettings.dev.json</c>. Environment variables are re-applied last so
    /// they keep the highest precedence over the sources added here.
    /// </summary>
    public static IConfigurationBuilder AddProjectConfiguration(this IConfigurationBuilder configBuilder, string? environmentName)
    {
        if (OperatingSystem.IsLinux())
        {
            configBuilder.AddJsonFile(
                Path.Combine(LinuxConfigDirectory, "appsettings.json"), optional: true, reloadOnChange: true);

            if (!string.IsNullOrWhiteSpace(environmentName))
            {
                configBuilder.AddJsonFile(
                    Path.Combine(LinuxConfigDirectory, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: true);
            }
        }

        configBuilder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
        configBuilder.AddEnvironmentVariables();

        return configBuilder;
    }
}
