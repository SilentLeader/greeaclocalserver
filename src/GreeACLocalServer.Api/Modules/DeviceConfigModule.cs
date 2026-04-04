using Microsoft.AspNetCore.Mvc;

namespace GreeACLocalServer.Api.Modules;

internal static class DeviceConfigModule
{
    /// <summary>
    /// Device configuration endpoints
    /// </summary>
    public static IEndpointRouteBuilder ConfigureDeviceConfigModule(this IEndpointRouteBuilder api)
    {
        var deviceConfig = api.MapGroup("/device-config");
        deviceConfig.MapPost("/status", async ([FromBody] QueryDeviceStatusRequest request, IDeviceConfigService configService, CancellationToken cancellationToken) =>
        {
            var result = await configService.QueryDeviceStatusAsync(request, cancellationToken);
            return Results.Ok(result);
        });
        deviceConfig.MapPost("/set-name", async ([FromBody] UpdateDeviceNameRequest request, IDeviceConfigService configService, CancellationToken cancellationToken) =>
        {
            var result = await configService.SetDeviceNameAsync(request, cancellationToken);
            return Results.Ok(result);
        });
        deviceConfig.MapPost("/set-remote-host", async ([FromBody] UpdateRemoteHostRequest request, IDeviceConfigService configService, CancellationToken cancellationToken) =>
        {
            var result = await configService.SetRemoteHostAsync(request, cancellationToken);
            return Results.Ok(result);
        });

        return api;
    }
}