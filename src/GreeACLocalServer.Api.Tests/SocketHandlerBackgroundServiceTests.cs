using System;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using Microsoft.Extensions.Logging;
using Moq;
using GreeACLocalServer.Api.Interfaces;
using GreeACLocalServer.Api.Services;

namespace GreeACLocalServer.Api.Tests;

public class SocketHandlerBackgroundServiceTests
{
    private sealed class FakeDeviceEventHandlerService : IDeviceEventHandlerService
    {
        public event EventHandler<DeviceConnectedMessage>? OnDeviceConnected;

        public void RaiseDeviceConnected(DeviceConnectedMessage message)
            => OnDeviceConnected?.Invoke(this, message);
    }

    private readonly Mock<ISocketHandlerService> _socketHandler = new();
    private readonly Mock<IInternalDeviceManagerService> _deviceManager = new();
    private readonly Mock<ILogger<SocketHandlerBackgroundService>> _logger = new();
    private readonly FakeDeviceEventHandlerService _eventHandler = new();

    private SocketHandlerBackgroundService CreateService() => new(
        _socketHandler.Object, _deviceManager.Object, _eventHandler, _logger.Object);

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.UtcNow;
        while (!condition() && (DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task DeviceConnectedEvent_IsProcessed_Once()
    {
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await service.Subscribed;
        _eventHandler.RaiseDeviceConnected(new DeviceConnectedMessage { MacAddress = "mac", IPAddress = "ip" });

        await WaitForAsync(() => _deviceManager.Invocations.Count > 0);
        _deviceManager.Verify(x => x.UpdateOrAddAsync("mac", "ip", 0, false), Times.Once);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DeviceConnectedEvent_PropagatesPortAndTlsFlag()
    {
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        await service.Subscribed;
        _eventHandler.RaiseDeviceConnected(new DeviceConnectedMessage
        {
            MacAddress = "mac",
            IPAddress = "ip",
            Port = 1813,
            IsTls = true
        });

        await WaitForAsync(() => _deviceManager.Invocations.Count > 0);
        _deviceManager.Verify(x => x.UpdateOrAddAsync("mac", "ip", 1813, true), Times.Once);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FailingProcessing_IsLogged_AndConsumerKeepsRunning()
    {
        _deviceManager.Setup(x => x.UpdateOrAddAsync("bad", It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        await service.Subscribed;

        _eventHandler.RaiseDeviceConnected(new DeviceConnectedMessage { MacAddress = "bad", IPAddress = "ip" });
        _eventHandler.RaiseDeviceConnected(new DeviceConnectedMessage { MacAddress = "good", IPAddress = "ip" });

        await WaitForAsync(() => _deviceManager.Invocations.Count >= 2);

        _deviceManager.Verify(x => x.UpdateOrAddAsync("good", "ip"), Times.Once);
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_TerminatesConsumer_WithoutHang()
    {
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        await service.Subscribed;

        var stop = service.StopAsync(CancellationToken.None);
        var completed = await Task.WhenAny(stop, Task.Delay(2000));

        Assert.Same(stop, completed);
        _socketHandler.Verify(x => x.Stop(), Times.Once);
    }
}
