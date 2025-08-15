using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Shared.ValueObjects;

namespace GreeACLocalServer.Api.Hubs;

public class DeviceHub(IInternalDeviceManagerService deviceManager) : Hub
{
    private readonly IInternalDeviceManagerService _deviceManager = deviceManager;

    public override async Task OnConnectedAsync()
    {
        var devices = await _deviceManager.GetAllDeviceStatesAsync();
        await Clients.Caller.SendAsync(DeviceHubMethods.DevicesSnapshot, devices);
        await base.OnConnectedAsync();
    }
}
