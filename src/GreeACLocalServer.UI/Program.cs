using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.UI.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register HttpClient and our API-backed services
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IDeviceManagerService, HttpDeviceManagerService>();
builder.Services.AddScoped<IDeviceConfigService, HttpDeviceConfigService>();
builder.Services.AddScoped<IConfigService, HttpConfigService>();

// Register browser detection service
builder.Services.AddScoped<IBrowserDetectionService, ClientBrowserDetectionService>();

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
