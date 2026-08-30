using System.Threading.Channels;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Api.Services;

public class SocketHandlerBackgroundService(
    ISocketHandlerService socketHandlerService,
    IInternalDeviceManagerService deviceManagerService,
    IDeviceEventHandlerService deviceEventHandlerService,
    ILogger<SocketHandlerBackgroundService> logger) : BackgroundService
{
    private readonly ISocketHandlerService _socketHandlerService = socketHandlerService;

    private readonly TaskCompletionSource _subscribed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes once the service is subscribed to device-connected events. Intended for tests.</summary>
    public Task Subscribed => _subscribed.Task;

    private readonly Channel<DeviceConnectedMessage> _queue =
        Channel.CreateBounded<DeviceConnectedMessage>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        deviceEventHandlerService.OnDeviceConnected += OnDeviceConnected;
        _subscribed.TrySetResult();

        // Start() is non-blocking (binds listeners, spawns accept loops); call it directly
        // so a bind/TLS/config failure surfaces through ExecuteAsync instead of being swallowed.
        _socketHandlerService.Start();
        await ProcessQueueAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _socketHandlerService.Stop();
        deviceEventHandlerService.OnDeviceConnected -= OnDeviceConnected;
        _queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private void OnDeviceConnected(object? sender, DeviceConnectedMessage message)
        => _queue.Writer.TryWrite(message);

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var msg in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await deviceManagerService.UpdateOrAddAsync(msg.MacAddress, msg.IPAddress, msg.Port, msg.IsTls);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process device-connected event for {Mac}", msg.MacAddress);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested – stop consuming.
        }
    }
}
