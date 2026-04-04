namespace GreeACLocalServer.Api.Modules;

internal static class ServerConfigModule
{
    public static IEndpointRouteBuilder ConfigureServerConfigModule(this IEndpointRouteBuilder api)
    {
        var config = api.MapGroup("/config");
        config.MapGet("/server", async (IConfigService configService, CancellationToken cancellationToken) =>
        {
            var result = await configService.GetServerConfigAsync(cancellationToken);
            return Results.Ok(result);
        });

        return api;
    }
}