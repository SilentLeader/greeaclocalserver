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

    private static Mock<IInternalDeviceManagerService> ManagerReturning(params string[] targets)
    {
        var dms = new Mock<IInternalDeviceManagerService>();
        dms.Setup(x => x.GetRuntimeStatePollTargets(
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int>()))
            .Returns(targets);
        dms.Setup(x => x.GetRecentlyConnectedMacs(It.IsAny<TimeSpan>()))
            .Returns(targets);
        dms.Setup(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceDto?)null);
        return dms;
    }

    private static async Task RunOneCycleAsync(AcRuntimeStatePollingService service)
    {
        await service.StartAsync(CancellationToken.None);
        await service.FirstCycleCompleted.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PollsExactlyTheRuntimeStatePollTargets()
    {
        var dms = ManagerReturning("mac-a", "mac-b");

        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync("mac-a", It.IsAny<CancellationToken>()), Times.Once);
        dms.Verify(x => x.RefreshRuntimeStateAsync("mac-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PassesBackoffAndFailureCeilingFromOptions()
    {
        var dms = ManagerReturning("mac-a");
        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions
            {
                Enabled = true,
                PollIntervalSeconds = 3600,
                OnlineWindowMinutes = 5,
                FailureBackoffSeconds = 120,
                MaxConsecutiveFailures = 7
            });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.GetRuntimeStatePollTargets(
            TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(120), 7), Times.Once);
    }

    [Fact]
    public async Task NoTargets_DoesNotPoll()
    {
        var dms = ManagerReturning();
        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AllDevicesBackedOff_DoesNotPollThem()
    {
        // Recently connected, but none are poll targets this cycle (all in backoff).
        var dms = new Mock<IInternalDeviceManagerService>();
        dms.Setup(x => x.GetRuntimeStatePollTargets(
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int>()))
            .Returns(Array.Empty<string>());
        dms.Setup(x => x.GetRecentlyConnectedMacs(It.IsAny<TimeSpan>()))
            .Returns(new[] { "mac-a", "mac-b" });

        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600, MaxConsecutiveFailures = 0 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Disabled_DoesNotPoll()
    {
        var dms = new Mock<IInternalDeviceManagerService>();
        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = false, PollIntervalSeconds = 3600 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.GetRuntimeStatePollTargets(It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int>()), Times.Never);
        dms.Verify(x => x.RefreshRuntimeStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OneDeviceThrowing_DoesNotStopTheOthers()
    {
        var dms = ManagerReturning("bad", "good");
        dms.Setup(x => x.RefreshRuntimeStateAsync("bad", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var options = new OptionsMonitorStub<AcRuntimeStateOptions>(
            new AcRuntimeStateOptions { Enabled = true, PollIntervalSeconds = 3600, MaxDegreeOfParallelism = 1 });

        await RunOneCycleAsync(new AcRuntimeStatePollingService(dms.Object, options, NullLogger<AcRuntimeStatePollingService>.Instance));

        dms.Verify(x => x.RefreshRuntimeStateAsync("good", It.IsAny<CancellationToken>()), Times.Once);
    }
}
