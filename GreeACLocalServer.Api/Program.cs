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
                builder.Host.UseSystemd();
                builder.Host.UseWindowsService();
                builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                builder.Configuration.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);

                builder.Services.AddSingleton<CryptoService>();
                builder.Services.AddSingleton<MessageHandlerService>();
                builder.Services.AddSingleton<DeviceManagerService>();
                builder.Services.AddSingleton<SocketHandlerService>();
                var serverOptionsSection = builder.Configuration.GetSection("Server");
                builder.Services.Configure<ServerOptions>(serverOptionsSection);
                builder.Services.Configure<DeviceManagerOptions>(builder.Configuration.GetSection("DeviceManager"));
                builder.Services.AddHostedService<SocketHandlerBackgroundService>();

                var serverOptions = serverOptionsSection.Get<ServerOptions>()!;
                if (serverOptions.EnableUI)
                {
                    builder.Services.AddRazorComponents()
                        .AddInteractiveServerComponents()
                        .AddInteractiveWebAssemblyComponents();
                }

                var app = builder.Build();

                if (serverOptions.EnableUI)
                {
                    // Configure the HTTP request pipeline.
                    if (app.Environment.IsDevelopment())
                    {
                        app.UseWebAssemblyDebugging();
                    }
                    else
                    {
                        app.UseHsts();
                    }

                    app.UseHttpsRedirection();

                    app.UseAntiforgery();

                    app.MapStaticAssets();
                    app.MapRazorComponents<App>()
                        .AddInteractiveServerRenderMode()
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