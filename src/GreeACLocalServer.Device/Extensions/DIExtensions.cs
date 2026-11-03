using System.Text;
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
        // Set configuration. Required options are validated on start so a bad
        // configuration fails fast with a clear message instead of throwing on
        // every device packet.
        var greeConfig = configuration.GetSection("GreeServer");

        services.AddOptions<ServerOptions>()
            .Bind(greeConfig.GetSection("ServerOptions"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DomainName), "GreeServer:ServerOptions:DomainName is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ExternalIp), "GreeServer:ServerOptions:ExternalIp is required.")
            .ValidateOnStart();

        services.AddOptions<EncryptionOptions>()
            .Bind(greeConfig.GetSection("EncryptionOptions"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DefaultCryptoKey), "GreeServer:EncryptionOptions:DefaultCryptoKey is required.")
            .Validate(
                o => string.IsNullOrWhiteSpace(o.DefaultCryptoKey) || IsValidAesKeyLength(o.DefaultCryptoKey),
                "GreeServer:EncryptionOptions:DefaultCryptoKey must be 16, 24 or 32 bytes long (UTF-8). "
                + "Check for quotes or characters (such as '#' or '%') added or lost when the value was set.")
            .ValidateOnStart();

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

    // CryptoService uses the UTF-8 bytes of the key as the AES key, so only
    // AES-128/192/256 lengths work.
    private static bool IsValidAesKeyLength(string key)
        => Encoding.UTF8.GetByteCount(key) is 16 or 24 or 32;
}