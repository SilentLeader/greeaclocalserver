using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Api.Services;

public class SocketHandlerBackgroundService(
    ISocketHandlerService socketHandlerService,
    IInternalDeviceManagerService deviceManagerService,
    IDeviceEventHandlerService deviceEventHandlerService) : BackgroundService
{
    private readonly ISocketHandlerService _socketHandlerService = socketHandlerService;


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        deviceEventHandlerService.OnDeviceConnected += OnDeviceConnected;
        await Task.Run(() => _socketHandlerService.Start(), stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _socketHandlerService.Stop();
        deviceEventHandlerService.OnDeviceConnected -= OnDeviceConnected;
        await base.StopAsync(cancellationToken);
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedMessage message)
    {
        deviceManagerService.UpdateOrAddAsync(message.MacAddress, message.IPAddress);
    }
}
