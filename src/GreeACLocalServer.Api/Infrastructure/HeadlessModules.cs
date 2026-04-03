namespace GreeACLocalServer.Api.Infrastructure;

internal static class HeadlessModules
{
    public static IServiceCollection ConfigureHeadlessServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services.ConfigureCommonServices(configuration)
            // Headless DeviceManagerService without SignalR dependency
            .AddSingleton<IInternalDeviceManagerService, HeadlessDeviceManagerService>()
            .AddSingleton<IDeviceManagerService>(x => x.GetRequiredService<IInternalDeviceManagerService>());
    }
}