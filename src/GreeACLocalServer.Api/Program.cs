using GreeACLocalServer.Api.Components;
using GreeACLocalServer.Api.Hubs;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using GreeACLocalServer.Api.Infrastructure;
using GreeACLocalServer.Api.Modules;

namespace GreeACLocalServer.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .BuildConfiguration()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        try
        {
            // Read EnableUI setting early to decide which builder to use
            var serverOptions = config.GetSection("Server").Get<ServerOptions>()!;

            if (serverOptions.EnableUI)
            {
                Log.Information("Starting with Web Application (UI enabled)");
                await RunWithWebApplicationAsync(args);
            }
            else
            {
                Log.Information("Starting with Generic Host (headless mode)");
                await RunWithGenericHostAsync(args);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task RunWithWebApplicationAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();

        // Configure OS-specific hosting
        builder.Host.ConfigureHostingServices();

        // Configure additional configuration sources
        builder.Configuration.BuildConfiguration();

        // Configure common services
        builder.Services.ConfigureWebServices(builder.Configuration);

        var app = builder.Build();
        ConfigureWebApplication(app);

        await app.RunAsync();
    }

    private static async Task RunWithGenericHostAsync(string[] args)
    {
        var hostBuilder = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureAppConfiguration((_, config) => config.BuildConfiguration())
            .ConfigureServices((context, services) => services.ConfigureHeadlessServices(context.Configuration));

        // Configure OS-specific hosting
        hostBuilder.ConfigureHostingServices();

        var host = hostBuilder.Build();
        await host.RunAsync();
    }

    private static void ConfigureWebApplication(WebApplication app)
    {
        // IMPORTANT: UseForwardedHeaders must be called FIRST in production
        if (!app.Environment.IsDevelopment())
        {
            app.UseForwardedHeaders();
            // Log the forwarded headers for debugging
            app.Use(LogForwardedHeadersMiddleWare);
        }

        // Regsiter endpoints
        var api = app.MapGroup("/api");
        api
            // Device endpoints
            .ConfigureDeviceModule()
            // Device configuration endpoints            
            .ConfigureDeviceConfigModule()
            // Server configuration endpoints
            .ConfigureServerConfigModule();

        // Map SignalR hubs                    
        app.MapHub<DeviceHub>("/hubs/devices", options =>
        {
            options.AllowStatefulReconnects = true;
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseResponseCompression();
        }

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(UI._Imports).Assembly);
    }

    private static async Task LogForwardedHeadersMiddleWare(HttpContext context, Func<Task> next)
    {
        Log.Debug("Request Scheme: {Scheme}, Host: {Host}, Headers: {@Headers}",
            context.Request.Scheme,
            context.Request.Host,
            new
            {
                XForwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString(),
                XForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString(),
                XForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString()
            });

        await next();
    }

}