using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Models;
using GreeACLocalServer.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;
using GreeACLocalServer.Api.Hubs;
using GreeACLocalServer.Shared.ValueObjects;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Device manager service with SignalR support for UI notifications.
/// Inherits core functionality from HeadlessDeviceManagerService and adds real-time updates.
/// </summary>
public class DeviceManagerService(
    IHubContext<DeviceHub> hubContext,
    IDnsResolverService dnsResolver,
    GreeACLocalServer.Device.Interfaces.IDeviceControllerService deviceController,
    IFirmwareUpdateService? firmwareUpdateService = null,
    IOptionsMonitor<FirmwareUpdateOptions>? firmwareOptions = null)
    : HeadlessDeviceManagerService(dnsResolver, deviceController, firmwareUpdateService, firmwareOptions)
{
    private readonly IHubContext<DeviceHub> _hub = hubContext;

    /// <summary>
    /// Called when a device is updated or added. Sends SignalR notification to all connected clients.
    /// </summary>
    /// <param name="deviceState">The updated device state</param>
    protected override async Task OnDeviceUpdatedAsync(AcDeviceState deviceState)
    {
        // Send SignalR notification for device upsert. Cache-only: this runs on the
        // single-threaded device-event loop and must not block on a cloud lookup.
        var dto = await ProjectAsync(deviceState, allowRemoteFetch: false);
        await _hub.Clients.All.SendAsync(DeviceHubMethods.DeviceUpserted, dto);
    }

    /// <summary>
    /// Called when devices are removed due to timeout. Sends SignalR notifications for each removed device.
    /// </summary>
    /// <param name="removedMacAddresses">List of MAC addresses that were removed</param>
    protected override async Task OnDevicesRemovedAsync(List<string> removedMacAddresses)
    {
        // Send SignalR notifications for device removals
        var tasks = removedMacAddresses.Select(mac =>
            _hub.Clients.All.SendAsync(DeviceHubMethods.DeviceRemoved, mac));

        await Task.WhenAll(tasks);
    }
}
