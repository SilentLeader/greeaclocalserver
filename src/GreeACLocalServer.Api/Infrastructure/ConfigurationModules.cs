namespace GreeACLocalServer.Api.Infrastructure;

internal static class ConfigurationModules
{
    private const string LinuxConfigDirectory = "/etc/greeac-localserver";

    /// <summary>
    /// The single source of truth for the configuration pipeline, used by every
    /// entry point in <see cref="Program"/>. Precedence, low to high:
    /// <list type="number">
    ///   <item><c>appsettings.json</c></item>
    ///   <item><c>appsettings.{environmentName}.json</c></item>
    ///   <item><c>/etc/greeac-localserver/appsettings.json</c> (Linux only)</item>
    ///   <item><c>/etc/greeac-localserver/appsettings.{environmentName}.json</c> (Linux only)</item>
    ///   <item><c>appsettings.dev.json</c> (local developer convenience, git-ignored)</item>
    ///   <item>environment variables</item>
    ///   <item>command-line arguments — these override everything</item>
    /// </list>
    /// </summary>
    /// <param name="linuxConfigDirectory">
    /// Overrides the <c>/etc</c> directory. Only tests pass this (the real absolute
    /// path is not writable without root); production callers leave it <c>null</c>.
    /// </param>
    public static IConfigurationBuilder AddGreeConfiguration(
        this IConfigurationBuilder builder,
        string? environmentName,
        string[]? commandLineArgs,
        string? linuxConfigDirectory = null)
    {
        var etc = linuxConfigDirectory ?? LinuxConfigDirectory;

        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
        }

        if (OperatingSystem.IsLinux() || linuxConfigDirectory is not null)
        {
            builder.AddJsonFile(
                Path.Combine(etc, "appsettings.json"), optional: true, reloadOnChange: true);

            if (!string.IsNullOrWhiteSpace(environmentName))
            {
                builder.AddJsonFile(
                    Path.Combine(etc, $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: true);
            }
        }

        builder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

        builder.AddEnvironmentVariables();

        if (commandLineArgs is { Length: > 0 })
        {
            builder.AddCommandLine(commandLineArgs);
        }

        return builder;
    }
}
