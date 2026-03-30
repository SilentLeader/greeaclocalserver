using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GreeACLocalServer.Device.Extensions;

public static class DIExtensions
{
    public static IServiceCollection AddGreeServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Set configuration
        var greeConfig = configuration.GetSection("GreeServer");
        services.Configure<ServerOptions>(greeConfig.GetSection("ServerOptions"));
        services.Configure<EncryptionOptions>(greeConfig.GetSection("EncryptionOptions"));

        // Set services
        services.AddSingleton<ICryptoService, CryptoService>();
        services.AddSingleton<IMessageHandlerService, MessageHandlerService>();
        services.AddSingleton<IDeviceControllerService, DeviceControllerService>();
        services.AddSingleton<ISocketHandlerService, SocketHandlerService>();
        services.AddSingleton<EventHandlerService>();
        services.AddSingleton<IDeviceEventPublisher>(s => s.GetRequiredService<EventHandlerService>());
        services.AddSingleton<IDeviceEventHandlerService>(s => s.GetRequiredService<EventHandlerService>());
        return services;
    }
}