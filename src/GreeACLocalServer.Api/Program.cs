using GreeACLocalServer.Api.Components;
using GreeACLocalServer.Api.Hubs;
using GreeACLocalServer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using Serilog;

namespace GreeACLocalServer.Api
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var configBuilder = new ConfigurationBuilder();
            SetupConfig(configBuilder);
            var config = configBuilder.Build();

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

        private static void SetupConfig(IConfigurationBuilder configBuilder)
        {
            configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            if (OperatingSystem.IsLinux())
            {
                configBuilder.AddJsonFile("/etc/greeac-localserver/appsettings.json", optional: true, reloadOnChange: true);
            }
    
            configBuilder.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
        }

        private static async Task RunWithWebApplicationAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog();

            // Configure OS-specific hosting
            ConfigureOSSpecificHosting(builder.Host);


            // Configure additional configuration sources
            SetupConfig(builder.Configuration);

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
                    SetupConfig(config);
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
            services.AddSingleton<ICryptoService, CryptoService>();
            services.AddSingleton<MessageHandlerService>();
            services.AddSingleton<IDnsResolverService, DnsResolverService>();
            services.AddSingleton<SocketHandlerService>();
            services.AddScoped<IDeviceConfigService, DeviceConfigService>();
            services.AddScoped<IConfigService, ConfigService>();
            
            // Configuration options
            services.Configure<ServerOptions>(configuration.GetSection("Server"));
            
            // Background services
            services.AddHostedService<SocketHandlerBackgroundService>();

            // Configure forwarded headers from appsettings
            var forwardedHeadersConfig = configuration.GetSection("ForwardedHeaders").Get<ForwardedHeadersConfiguration>() ?? new ForwardedHeadersConfiguration();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                forwardedHeadersConfig.ApplyToForwardedHeadersOptions(options);
            });
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
            
            // Register server-side browser detection service
            services.AddScoped<IBrowserDetectionService, ServerBrowserDetectionService>();
            
            services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();
        }

        private static void ConfigureWebApplication(WebApplication app)
        {
            // IMPORTANT: UseForwardedHeaders must be called FIRST in production
            if (!app.Environment.IsDevelopment())
            {
                app.UseForwardedHeaders();
                // Log the forwarded headers for debugging
                app.Use(async (context, next) =>
                {
                    Log.Debug("Request Scheme: {Scheme}, Host: {Host}, Headers: {@Headers}", 
                        context.Request.Scheme, 
                        context.Request.Host,
                        new { 
                            XForwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString(),
                            XForwardedFor = context.Request.Headers["X-Forwarded-For"].ToString(),
                            XForwardedHost = context.Request.Headers["X-Forwarded-Host"].ToString()
                        });
                    await next();
                });
            }

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
            api.MapDelete("/devices/{mac}", async (string mac, IInternalDeviceManagerService dms) =>
            {
                var removed = await dms.RemoveDeviceAsync(mac);
                return removed
                    ? Results.Ok(new { Success = true, Message = $"Device {mac} removed successfully" })
                    : Results.NotFound(new { Success = false, Message = $"Device {mac} not found" });
            });
            
            // Device configuration endpoints
            var deviceConfig = api.MapGroup("/device-config");
            deviceConfig.MapPost("/status", async ([FromBody] DeviceStatusRequest request, IDeviceConfigService configService) =>
            {
                var result = await configService.QueryDeviceStatusAsync(request);
                return Results.Ok(result);
            });
            deviceConfig.MapPost("/set-name", async ([FromBody] SetDeviceNameRequest request, IDeviceConfigService configService) =>
            {
                var result = await configService.SetDeviceNameAsync(request);
                return Results.Ok(result);
            });
            deviceConfig.MapPost("/set-remote-host", async ([FromBody] SetRemoteHostRequest request, IDeviceConfigService configService) =>
            {
                var result = await configService.SetRemoteHostAsync(request);
                return Results.Ok(result);
            });

            // Server configuration endpoints
            var config = api.MapGroup("/config");
            config.MapGet("/server", async (IConfigService configService) =>
            {
                var result = await configService.GetServerConfigAsync();
                return Results.Ok(result);
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