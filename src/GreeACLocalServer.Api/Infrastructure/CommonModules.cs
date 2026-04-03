using GreeACLocalServer.Device.Extensions;

namespace GreeACLocalServer.Api.Infrastructure;

internal static class CommonModules
{
    public static IServiceCollection ConfigureCommonServices(this IServiceCollection services, IConfiguration configuration)
    {
        var forwardedHeadersConfig = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersConfiguration>() ?? new ForwardedHeadersConfiguration();

        // Core services needed in both scenarios            
        return services.AddGreeServices(configuration)
            .AddSingleton<IDnsResolverService, DnsResolverService>()
            .AddScoped<IDeviceConfigService, DeviceConfigService>()
            .AddScoped<IConfigService, ConfigService>()
            // Configuration options
            .Configure<ServerOptions>(configuration.GetSection("Server"))
            .Configure<DeviceManagerOptions>(configuration.GetSection("DeviceManager"))
            // Background services
            .AddHostedService<SocketHandlerBackgroundService>()
            // Configure forwarded headers from appsettings        
            .Configure<ForwardedHeadersOptions>(options =>
            {
                forwardedHeadersConfig.ApplyToForwardedHeadersOptions(options);
            });
    }
}