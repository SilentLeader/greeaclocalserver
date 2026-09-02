using System.Reflection;
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
        // Resolve the environment before any host builder exists so the early
        // Serilog / EnableUI bootstrap honours appsettings.{Environment}.json too.
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        var config = new ConfigurationBuilder()
            .AddGreeConfiguration(environmentName, args)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        LogStartupBanner(environmentName);

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

    private static void LogStartupBanner(string? environmentName)
    {
        var asm = typeof(Program).Assembly;
        var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";

        Log.Information(
            "GreeAC Local Server {Version} starting (environment: {Environment})",
            version,
            string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName);
    }

    private static async Task RunWithWebApplicationAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();

        // Configure OS-specific hosting
        builder.Host.ConfigureHostingServices();

        // Take full control of the configuration pipeline: drop the host builder's
        // default sources and re-add them in our documented precedence, with the
        // command line last so it overrides everything.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddGreeConfiguration(builder.Environment.EnvironmentName, args);

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
            .ConfigureAppConfiguration((context, config) =>
            {
                config.Sources.Clear();
                config.AddGreeConfiguration(context.HostingEnvironment.EnvironmentName, args);
            })
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
            app.Use(LogForwardedHeadersMiddleware);
        }

        // Register endpoints
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

    private static async Task LogForwardedHeadersMiddleware(HttpContext context, Func<Task> next)
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