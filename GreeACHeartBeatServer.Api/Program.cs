using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using GreeACHeartBeatServer.Api.Services;
using GreeACHeartBeatServer.Api.Options;
using Serilog;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace GreeACHeartBeatServer.Api
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true)
                    .Build())
                .CreateLogger();

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile("appsettings.dev.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<CryptoService>();
                    services.AddSingleton<MessageHandlerService>();
                    services.AddSingleton<SocketHandlerService>();
                    services.Configure<ServerOptions>(context.Configuration.GetSection("Server"));
                    services.AddHostedService<SocketHandlerBackgroundService>();
                })
                .UseSystemd()
                .UseWindowsService();

            var host = hostBuilder.Build();
            host.Run();
        }
    }
}