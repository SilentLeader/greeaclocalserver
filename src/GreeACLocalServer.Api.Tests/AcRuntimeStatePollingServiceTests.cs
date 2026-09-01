using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Interfaces;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GreeACLocalServer.Api.Tests;

public class AcRuntimeStatePollingServiceTests
{
    private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; set; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static async Task RunOneCycleAsync(AcRuntimeStatePollingService service)
    {
        await service.StartAsync(CancellationToken.None);
        await service.FirstCycleCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PollsExactlyTheRecentlyConnectedDevices()
    {
        var dms = new Mock<IInternalDeviceManagerService>();
        dms.Setup(x => x.GetRecentlyConnectedMacs(It.IsAny<TimeSpan>()))
            .Returns(new[] { "mac-a", "mac-b" });
        dms.Setup(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceDto?)null);

        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync("mac-a", It.IsAny<CancellationToken>()), Times.Once);
        dms.Verify(x => x.RefreshRuntimeStateAsync("mac-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disabled_DoesNotPoll()
    {
        var dms = new Mock<IInternalDeviceManagerService>();
        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = false, PollIntervalSeconds = 3600 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.GetRecentlyConnectedMacs(It.IsAny<TimeSpan>()), Times.Never);
        dms.Verify(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OneDeviceThrowing_DoesNotStopTheOthers()
    {
        var dms = new Mock<IInternalDeviceManagerService>();
        dms.Setup(x => x.GetRecentlyConnectedMacs(It.IsAny<TimeSpan>()))
            .Returns(new[] { "bad", "good" });
        dms.Setup(x => x.RefreshRuntimeStateAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        dms.Setup(x => x.RefreshRuntimeStateAsync("good", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceDto?)null);

        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600, MaxDegreeOfParallelism = 1 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync("good", It.IsAny<CancellationToken>()), Times.Once);
    }
}
