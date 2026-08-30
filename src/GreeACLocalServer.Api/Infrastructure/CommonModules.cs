using GreeACLocalServer.Device.Extensions;

namespace GreeACLocalServer.Api.Infrastructure;

internal static class CommonModules
{
    public static IServiceCollection ConfigureCommonServices(this IServiceCollection services, IConfiguration configuration)
    {
        var forwardedHeadersConfig = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersConfiguration>() ?? new ForwardedHeadersConfiguration();

        // Core services needed in both scenarios            
        services.AddHttpClient(FirmwareUpdateService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services.AddGreeServices(configuration)
            .AddMemoryCache(options => options.SizeLimit = 4096)
            .AddSingleton<IDnsResolverService, DnsResolverService>()
            .AddSingleton<IFirmwareUpdateService, FirmwareUpdateService>()
            .AddScoped<IDeviceConfigService, DeviceConfigService>()
            .AddScoped<IConfigService, ConfigService>()
            // Configuration options
            .Configure<ServerOptions>(configuration.GetSection("Server"))
            .Configure<DeviceManagerOptions>(configuration.GetSection("DeviceManager"))
            .Configure<FirmwareUpdateOptions>(configuration.GetSection("GreeServer:FirmwareUpdateCheck"))
            // Background services
            .AddHostedService<SocketHandlerBackgroundService>()
            // Configure forwarded headers from appsettings        
            .Configure<ForwardedHeadersOptions>(options =>
            {
                forwardedHeadersConfig.ApplyToForwardedHeadersOptions(options);
            });
    }
}