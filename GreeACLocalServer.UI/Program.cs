using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System;
using System.Net.Http;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.UI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register HttpClient and our API-backed device manager service
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IDeviceManagerService, HttpDeviceManagerService>();

await builder.Build().RunAsync();
