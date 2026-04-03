using MudBlazor.Services;

namespace GreeACLocalServer.Api.Infrastructure;

internal static class WebModules
{
    public static IServiceCollection ConfigureWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .ConfigureCommonServices(configuration)
            // UI-enabled DeviceManagerService with SignalR support
            .AddSingleton<IInternalDeviceManagerService, DeviceManagerService>()
            .AddSingleton<IDeviceManagerService>(x => x.GetRequiredService<IInternalDeviceManagerService>())
            .AddScoped<ILocalStorageService, LocalStorageService>()
            .AddScoped<IThemeService, ThemeService>()
            // Web-specific services
            .AddEndpointsApiExplorer()
            .AddResponseCompression()
            .AddMudServices()
            // Register server-side browser detection service
            //services.AddScoped<IBrowserDetectionService, ServerBrowserDetectionService>();
            .AddScoped<IBrowserDetectionService, ClientBrowserDetectionService>();

        services.AddSignalR();
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        return services;
    }
}