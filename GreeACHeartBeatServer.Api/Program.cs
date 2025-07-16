using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using GreeACHeartBeatServer.Api.Services;
using GreeACHeartBeatServer.Api.Options;
using Serilog;

namespace GreeACHeartBeatServer.Api
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build())
                .CreateLogger();

            var host = Host.CreateDefaultBuilder(args)
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
                })
                .Build();

            var gsh = host.Services.GetRequiredService<SocketHandlerService>();
            gsh.Start();
        }
    }
}