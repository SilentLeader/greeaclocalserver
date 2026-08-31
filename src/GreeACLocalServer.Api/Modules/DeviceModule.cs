namespace GreeACLocalServer.Api.Modules;

internal static class DeviceModule
{
    /// <summary>
    /// Device endpoints
    /// </summary>
    public static IEndpointRouteBuilder ConfigureDeviceModule(this IEndpointRouteBuilder api)
    {
        api.MapGet("/devices", async (IInternalDeviceManagerService dms, CancellationToken cancellationToken) =>
        {
            var list = await dms.GetAllDeviceStatesAsync(cancellationToken);
            return Results.Ok(list);
        });
        api.MapGet("/devices/{mac}", async (string mac, IInternalDeviceManagerService dms, CancellationToken cancellationToken) =>
        {
            var device = await dms.GetAsync(mac, cancellationToken);
            return device is null
                ? Results.NotFound()
                : Results.Ok(device);
        });
        api.MapPost("/devices/{mac}/refresh-firmware", async (string mac, IInternalDeviceManagerService dms, CancellationToken cancellationToken) =>
        {
            var device = await dms.GetAsync(mac, cancellationToken);
            if (device is null)
            {
                return Results.NotFound(new { Success = false, Message = $"Device {mac} not found" });
            }

            var refreshed = await dms.RefreshFirmwareAsync(mac, cancellationToken);
            return refreshed is null
                ? Results.Json(new { Success = false, Message = "Could not query firmware from the device" }, statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(refreshed);
        });
        api.MapPost("/devices/{mac}/refresh-state", async (string mac, IInternalDeviceManagerService dms, CancellationToken cancellationToken) =>
        {
            var device = await dms.GetAsync(mac, cancellationToken);
            if (device is null)
            {
                return Results.NotFound(new { Success = false, Message = $"Device {mac} not found" });
            }

            var refreshed = await dms.RefreshRuntimeStateAsync(mac, cancellationToken);
            return refreshed is null
                ? Results.Json(new { Success = false, Message = "Could not query operating state from the device" }, statusCode: StatusCodes.Status503ServiceUnavailable)
                : Results.Ok(refreshed);
        });
        api.MapDelete("/devices/{mac}", async (string mac, IInternalDeviceManagerService dms, CancellationToken cancellationToken) =>
        {
            var removed = await dms.RemoveDeviceAsync(mac, cancellationToken);
            return removed
                ? Results.Ok(new { Success = true, Message = $"Device {mac} removed successfully" })
                : Results.NotFound(new { Success = false, Message = $"Device {mac} not found" });
        });

        return api;
    }
}