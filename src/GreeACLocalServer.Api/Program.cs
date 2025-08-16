using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Api.Options;
using Serilog;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;
using System;
using GreeACLocalServer.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using GreeACLocalServer.Api.Components;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.Api.Hubs;
using MudBlazor.Services;
using Microsoft.AspNetCore.HttpOverrides;

namespace GreeACLocalServer.Api
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build())
                .CreateLogger();

            try
            {
                // Read EnableUI setting early to decide which builder to use
                var tempConfig = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                var serverOptions = tempConfig.GetSection("Server").Get<ServerOptions>()!;

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
            ConfigureOSSpecificHosting(builder.Host);
            
            // Configure additional configuration sources
            builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            builder.Configuration.AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true);
            builder.Configuration.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

            // Configure common services
            ConfigureCommonServicesWithUI(builder.Services, builder.Configuration);

            // Configure web-specific services
            ConfigureWebServices(builder.Services);

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
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureCommonServicesHeadless(services, context.Configuration);
                });

            // Configure OS-specific hosting
            ConfigureOSSpecificHosting(hostBuilder);

            var host = hostBuilder.Build();
            await host.RunAsync();
        }

        private static void ConfigureOSSpecificHosting(IHostBuilder hostBuilder)
        {
            if (OperatingSystem.IsLinux())
            {
                hostBuilder.UseSystemd();
            }
            if (OperatingSystem.IsWindows())
            {
                hostBuilder.UseWindowsService();
            }
        }

        private static void ConfigureCommonServices(IServiceCollection services, IConfiguration configuration)
        {
            // Core services needed in both scenarios
            services.AddSingleton<CryptoService>();
            services.AddSingleton<MessageHandlerService>();
            services.AddSingleton<IDnsResolverService, DnsResolverService>();
            services.AddSingleton<SocketHandlerService>();
            
            // Configuration options
            services.Configure<ServerOptions>(configuration.GetSection("Server"));
            services.Configure<DeviceManagerOptions>(configuration.GetSection("DeviceManager"));
            
            // Background services
            services.AddHostedService<SocketHandlerBackgroundService>();
        }

        private static void ConfigureCommonServicesWithUI(IServiceCollection services, IConfiguration configuration)
        {
            ConfigureCommonServices(services, configuration);
            
            // UI-enabled DeviceManagerService with SignalR support
            services.AddSingleton<IInternalDeviceManagerService, DeviceManagerService>();
            services.AddSingleton<IDeviceManagerService>(x => x.GetRequiredService<IInternalDeviceManagerService>());
        }

        private static void ConfigureCommonServicesHeadless(IServiceCollection services, IConfiguration configuration)
        {
            ConfigureCommonServices(services, configuration);
            
            // Headless DeviceManagerService without SignalR dependency
            services.AddSingleton<IInternalDeviceManagerService, HeadlessDeviceManagerService>();
            services.AddSingleton<IDeviceManagerService>(x => x.GetRequiredService<IInternalDeviceManagerService>());
        }

        private static void ConfigureWebServices(IServiceCollection services)
        {
            // Web-specific services
            services.AddSignalR();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddResponseCompression();
            services.AddMudServices();
            
            services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();
        }

        private static void ConfigureWebApplication(WebApplication app)
        {
            // Minimal API endpoints under /api
            var api = app.MapGroup("/api");
            api.MapGet("/devices", async (IInternalDeviceManagerService dms) =>
            {
                var list = await dms.GetAllDeviceStatesAsync();
                return Results.Ok(list);
            });
            api.MapGet("/devices/{mac}", async (string mac, IInternalDeviceManagerService dms) =>
            {
                var device = await dms.GetAsync(mac);
                return device is null
                    ? Results.NotFound()
                    : Results.Ok(device);
            });

            // Map SignalR hubs                    
            app.MapHub<DeviceHub>("/hubs/devices", options =>
            {
                options.AllowStatefulReconnects = true;
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseResponseCompression();
                app.UseHsts();
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                });
                app.UseHttpsRedirection();
            }

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()   
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(GreeACLocalServer.UI._Imports).Assembly);
        }
    }
}