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

namespace GreeACLocalServer.Api
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                    .Build())
                .CreateLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);
                builder.Host.UseSerilog();
                // Gate service hosting integration by OS
                if (OperatingSystem.IsLinux())
                {
                    builder.Host.UseSystemd();
                }
                if (OperatingSystem.IsWindows())
                {
                    builder.Host.UseWindowsService();
                }
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                builder.Configuration.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

                builder.Services.AddSingleton<CryptoService>();
                builder.Services.AddSingleton<MessageHandlerService>();
                builder.Services.AddSingleton<IInternalDeviceManagerService, DeviceManagerService>();
                builder.Services.AddSingleton<IDeviceManagerService>(x => x.GetRequiredService<IInternalDeviceManagerService>());
                builder.Services.AddSingleton<SocketHandlerService>();
                var serverOptionsSection = builder.Configuration.GetSection("Server");
                builder.Services.Configure<ServerOptions>(serverOptionsSection);
                builder.Services.Configure<DeviceManagerOptions>(builder.Configuration.GetSection("DeviceManager"));
                builder.Services.AddHostedService<SocketHandlerBackgroundService>();

                var serverOptions = serverOptionsSection.Get<ServerOptions>()!;
                if (serverOptions.EnableUI)
                {
                    // SignalR
                    builder.Services.AddSignalR();

                    // Minimal APIs support and Swagger for dev
                    builder.Services.AddEndpointsApiExplorer();
                    builder.Services.AddSwaggerGen();
                    
                    // Add MudBlazor services
                    builder.Services.AddMudServices();
                    
                    builder.Services.AddRazorComponents()
                        .AddInteractiveServerComponents()
                        .AddInteractiveWebAssemblyComponents();
                }

                var app = builder.Build();

                if (serverOptions.EnableUI)
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
                    }
                    else
                    {
                        app.UseResponseCompression();
                    }

                    // Configure the HTTP request pipeline.
                    if (app.Environment.IsDevelopment())
                    {
                        app.UseWebAssemblyDebugging();
                    }
                    else
                    {
                        app.UseHsts();
                        app.UseHttpsRedirection();
                    }

                    app.UseAntiforgery();

                    app.MapStaticAssets();
                    app.MapRazorComponents<App>()
                        .AddInteractiveServerRenderMode(o => o.DisableWebSocketCompression = true)
                        .AddInteractiveWebAssemblyRenderMode()
                        .AddAdditionalAssemblies(typeof(GreeACLocalServer.UI._Imports).Assembly);
                }

                await app.RunAsync();
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
    }
}