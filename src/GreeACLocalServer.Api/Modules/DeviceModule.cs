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