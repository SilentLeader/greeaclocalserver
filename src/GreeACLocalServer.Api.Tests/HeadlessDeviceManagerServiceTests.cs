using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using GreeACLocalServer.Api.Interfaces;
using GreeACLocalServer.Api.Models;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Results;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.ValueObjects;

namespace GreeACLocalServer.Api.Tests;

public class HeadlessDeviceManagerServiceTests
{
    private readonly Mock<IDnsResolverService> _mockDnsResolver;
    private readonly Mock<IDeviceControllerService> _mockDeviceController;
    private readonly HeadlessDeviceManagerService _deviceManagerService;

    public HeadlessDeviceManagerServiceTests()
    {
        _mockDnsResolver = new Mock<IDnsResolverService>();
        _mockDnsResolver.Setup(x => x.ResolveDnsNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string ip) => $"device-{ip.Replace(".", "-")}.local");

        _mockDeviceController = new Mock<IDeviceControllerService>();

        _deviceManagerService = new HeadlessDeviceManagerService(_mockDnsResolver.Object, _mockDeviceController.Object);
    }

    [Fact]
    public async Task UpdateOrAddAsync_WithNewDevice_AddsDevice()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";

        // Act
        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);

        // Assert
        var device = await _deviceManagerService.GetAsync(macAddress);
        Assert.NotNull(device);
        Assert.Equal(macAddress, device.MacAddress);
        Assert.Equal(ipAddress, device.IpAddress);
        Assert.Equal("device-192-168-1-100.local", device.DNSName);
    }

    [Fact]
    public async Task UpdateOrAddAsync_WithExistingDevice_UpdatesDevice()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress1 = "192.168.1.100";
        var ipAddress2 = "192.168.1.101";

        // Act
        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress1);
        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress2);

        // Assert
        var device = await _deviceManagerService.GetAsync(macAddress);
        Assert.NotNull(device);
        Assert.Equal(macAddress, device.MacAddress);
        Assert.Equal(ipAddress2, device.IpAddress);
        Assert.Equal("device-192-168-1-101.local", device.DNSName);
    }

    [Fact]
    public async Task GetAllDeviceStatesAsync_WithMultipleDevices_ReturnsAllDevices()
    {
        // Arrange
        var macAddress1 = "AA:BB:CC:DD:EE:FF";
        var macAddress2 = "11:22:33:44:55:66";
        var ipAddress1 = "192.168.1.100";
        var ipAddress2 = "192.168.1.101";

        await _deviceManagerService.UpdateOrAddAsync(macAddress1, ipAddress1);
        await _deviceManagerService.UpdateOrAddAsync(macAddress2, ipAddress2);

        // Act
        var devices = await _deviceManagerService.GetAllDeviceStatesAsync();

        // Assert
        Assert.Equal(2, devices.Count());
        Assert.Contains(devices, d => d.MacAddress == macAddress1);
        Assert.Contains(devices, d => d.MacAddress == macAddress2);
    }

    [Fact]
    public async Task GetAsync_WithNonExistentDevice_ReturnsNull()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";

        // Act
        var device = await _deviceManagerService.GetAsync(macAddress);

        // Assert
        Assert.Null(device);
    }

    [Fact]
    public async Task UpdateOrAddAsync_CalledTwiceForSameMac_KeepsSingleUpdatedEntry()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";

        // Act
        await _deviceManagerService.UpdateOrAddAsync(macAddress, "192.168.1.100");
        var firstSeen = (await _deviceManagerService.GetAsync(macAddress))!.LastConnectionTimeUtc;
        await Task.Delay(10);
        await _deviceManagerService.UpdateOrAddAsync(macAddress, "192.168.1.101");

        // Assert
        var devices = (await _deviceManagerService.GetAllDeviceStatesAsync()).ToList();
        Assert.Single(devices);
        Assert.Equal("192.168.1.101", devices[0].IpAddress);
        Assert.True(devices[0].LastConnectionTimeUtc >= firstSeen);
    }

    [Fact]
    public async Task UpdateOrAddAsync_DoesNotMutateSnapshotObservedByReaders()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(macAddress, "192.168.1.100");
        var snapshot = await _deviceManagerService.GetAsync(macAddress);

        // Act - a later update must not retroactively change an earlier snapshot
        await _deviceManagerService.UpdateOrAddAsync(macAddress, "192.168.1.200");

        // Assert
        Assert.Equal("192.168.1.100", snapshot!.IpAddress);
    }

    [Fact]
    public async Task UpdateOrAddAsync_ConcurrentWithReads_IsConsistent()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act
        for (var i = 0; i < 100; i++)
        {
            var mac = $"AA:BB:CC:DD:EE:{i:X2}";
            var ip = $"192.168.1.{i}";
            tasks.Add(Task.Run(() => _deviceManagerService.UpdateOrAddAsync(mac, ip)));
            tasks.Add(Task.Run(() => _deviceManagerService.GetAllDeviceStatesAsync()));
        }

        // Assert - no exception, all writes land exactly once
        await Task.WhenAll(tasks);
        var devices = (await _deviceManagerService.GetAllDeviceStatesAsync()).ToList();
        Assert.Equal(100, devices.Count);
        Assert.Equal(100, devices.Select(d => d.MacAddress).Distinct().Count());
    }

    [Fact]
    public async Task UpdateOrAddAsync_CallsDnsResolver()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";

        // Act
        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);

        // Assert
        _mockDnsResolver.Verify(x => x.ResolveDnsNameAsync(ipAddress), Times.Once);
    }

    [Fact]
    public async Task GetAllDeviceStatesAsync_DoesNotRemoveStaleDevicesAutomatically()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";
        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);

        // Reset DNS resolver call count
        _mockDnsResolver.Reset();

        // Act
        var devices = await _deviceManagerService.GetAllDeviceStatesAsync();

        // Assert - Should not call DNS resolver since we removed automatic stale device removal
        _mockDnsResolver.Verify(x => x.ResolveDnsNameAsync(It.IsAny<string>()), Times.Never);
        Assert.Single(devices);
        Assert.Equal(macAddress, devices.First().MacAddress);
    }

    [Fact]
    public async Task UpdateOrAddAsync_RecordsConnectionEndpoint()
    {
        var mac = "AA:BB:CC:DD:EE:FF";

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 5000, isTls: false);

        var device = await _deviceManagerService.GetAsync(mac);
        var endpoint = Assert.Single(device!.Endpoints);
        Assert.Equal(5000, endpoint.Port);
        Assert.False(endpoint.IsTls);
    }

    [Fact]
    public async Task UpdateOrAddAsync_SamePortAndTls_RefreshesSingleEndpoint()
    {
        var mac = "AA:BB:CC:DD:EE:FF";

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 5000, isTls: false);
        var firstSeen = (await _deviceManagerService.GetAsync(mac))!.Endpoints[0].LastSeenUtc;
        await Task.Delay(10);
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 5000, isTls: false);

        var device = await _deviceManagerService.GetAsync(mac);
        var endpoint = Assert.Single(device!.Endpoints);
        Assert.True(endpoint.LastSeenUtc >= firstSeen);
    }

    [Fact]
    public async Task UpdateOrAddAsync_DistinctPortsAndProtocols_AreTrackedSeparatelyAndOrdered()
    {
        var mac = "AA:BB:CC:DD:EE:FF";

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 1813, isTls: true);
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 5000, isTls: false);
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 1812, isTls: false);

        var device = await _deviceManagerService.GetAsync(mac);
        Assert.Equal(
            new[] { (1812, false), (1813, true), (5000, false) },
            device!.Endpoints.Select(e => (e.Port, e.IsTls)));
    }

    [Fact]
    public async Task UpdateOrAddAsync_WithUnknownPort_RecordsNoEndpoint()
    {
        var mac = "AA:BB:CC:DD:EE:FF";

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100", port: 0, isTls: false);

        var device = await _deviceManagerService.GetAsync(mac);
        Assert.Empty(device!.Endpoints);
    }

    [Fact]
    public void Constructor_WithNullDnsResolver_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - Null DNS resolver doesn't fail immediately in constructor
        var service = new HeadlessDeviceManagerService(null!, _mockDeviceController.Object);
        Assert.NotNull(service);

        // But would fail when actually using DNS resolution
        // We don't test that here as it would require async testing
    }

    [Fact]
    public async Task RefreshFirmwareAsync_KnownDevice_StoresFirmwareAndExposesItOnDto()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");

        _mockDeviceController
            .Setup(x => x.GetDeviceFirmwareAsync(It.IsAny<GreeACLocalServer.Device.Requests.GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GreeACLocalServer.Device.Results.DeviceFirmwareResult(
                true, string.Empty, hid: "362001065736+U-QCOM4004CV3.76.bin",
                firmwareVersion: "3.76", firmwareCode: "362001065736", macAddress: mac));

        var refreshed = await _deviceManagerService.RefreshFirmwareAsync(mac);

        Assert.NotNull(refreshed);
        Assert.Equal("3.76", refreshed!.FirmwareVersion);
        Assert.Equal("362001065736", refreshed.FirmwareCode);

        var reloaded = await _deviceManagerService.GetAsync(mac);
        Assert.Equal("3.76", reloaded!.FirmwareVersion);
    }

    [Fact]
    public async Task RefreshFirmwareAsync_UnknownDevice_ReturnsNull()
    {
        var result = await _deviceManagerService.RefreshFirmwareAsync("00:00:00:00:00:00");
        Assert.Null(result);
    }

    // ---- Runtime (operating) state ----

    private void SetupRuntimeState(DeviceRuntimeStateResult result) =>
        _mockDeviceController
            .Setup(x => x.GetDeviceRuntimeStateAsync(It.IsAny<GreeACLocalServer.Device.Requests.GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task RefreshRuntimeStateAsync_Success_StampsStateOnDto()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 1, targetTemperature: 23, temperatureUnit: 0, currentTemperatureRaw: 65, macAddress: mac));

        var refreshed = await _deviceManagerService.RefreshRuntimeStateAsync(mac);

        Assert.NotNull(refreshed!.RuntimeState);
        Assert.True(refreshed.RuntimeState!.Power);
        Assert.Equal(AcMode.Cool, refreshed.RuntimeState.Mode);
        Assert.Equal(23, refreshed.RuntimeState.TargetTemperature);
        Assert.Equal(AcTemperatureUnit.Celsius, refreshed.RuntimeState.TemperatureUnit);
        Assert.Equal(25, refreshed.RuntimeState.CurrentTemperature);

        var reloaded = await _deviceManagerService.GetAsync(mac);
        Assert.Equal(AcMode.Cool, reloaded!.RuntimeState!.Mode);
    }

    [Fact]
    public async Task RefreshRuntimeStateAsync_OutOfRangeMode_MapsToUnknown()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 9, targetTemperature: 20, temperatureUnit: 1, macAddress: mac));

        var refreshed = await _deviceManagerService.RefreshRuntimeStateAsync(mac);

        Assert.Equal(AcMode.Unknown, refreshed!.RuntimeState!.Mode);
        Assert.Equal(AcTemperatureUnit.Fahrenheit, refreshed.RuntimeState.TemperatureUnit);
    }

    [Theory]
    [InlineData(65, 25)]     // raw carries a +40 offset
    [InlineData(60, 20)]
    [InlineData(0, null)]    // devices without a sensor report 0
    [InlineData(178, null)]  // implausible (seen on some U Crown units) -> rejected
    public async Task RefreshRuntimeStateAsync_CurrentTemperature_OffsetAndRangeChecked(int raw, int? expected)
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 1, targetTemperature: 23, temperatureUnit: 0, currentTemperatureRaw: raw, macAddress: mac));

        var refreshed = await _deviceManagerService.RefreshRuntimeStateAsync(mac);

        Assert.Equal(expected, refreshed!.RuntimeState!.CurrentTemperature);
    }

    [Fact]
    public async Task RefreshRuntimeStateAsync_QueryFails_ClearsStateAndReturnsNull()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 1, targetTemperature: 23, temperatureUnit: 0, macAddress: mac));
        await _deviceManagerService.RefreshRuntimeStateAsync(mac);

        SetupRuntimeState(new DeviceRuntimeStateResult(false, "NO_RESPONSE", "NO_RESPONSE"));
        var result = await _deviceManagerService.RefreshRuntimeStateAsync(mac);

        Assert.Null(result);
        var reloaded = await _deviceManagerService.GetAsync(mac);
        Assert.Null(reloaded!.RuntimeState);
    }

    [Fact]
    public async Task RefreshRuntimeStateAsync_UnknownDevice_ReturnsNull()
    {
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 1, targetTemperature: 23, temperatureUnit: 0));

        Assert.Null(await _deviceManagerService.RefreshRuntimeStateAsync("00:00:00:00:00:00"));
    }

    [Fact]
    public async Task RefreshRuntimeStateAsync_UnchangedReading_DoesNotNotifyAgain()
    {
        var manager = new PushCountingManager(_mockDnsResolver, _mockDeviceController);
        await manager.UpdateOrAddAsync("AA:BB:CC:DD:EE:FF", "192.168.1.100");
        SetupRuntimeState(new DeviceRuntimeStateResult(true, string.Empty,
            power: true, mode: 1, targetTemperature: 23, temperatureUnit: 0, macAddress: "AA:BB:CC:DD:EE:FF"));

        await manager.RefreshRuntimeStateAsync("AA:BB:CC:DD:EE:FF");
        manager.RuntimeStatePushes = 0;
        await manager.RefreshRuntimeStateAsync("AA:BB:CC:DD:EE:FF");

        Assert.Equal(0, manager.RuntimeStatePushes);
    }

    [Fact]
    public async Task GetRecentlyConnectedMacs_FiltersByWindow()
    {
        await _deviceManagerService.UpdateOrAddAsync("AA:BB:CC:DD:EE:01", "192.168.1.101");
        await _deviceManagerService.UpdateOrAddAsync("AA:BB:CC:DD:EE:02", "192.168.1.102");

        var recent = _deviceManagerService.GetRecentlyConnectedMacs(TimeSpan.FromMinutes(5));
        Assert.Equal(2, recent.Count);

        var none = _deviceManagerService.GetRecentlyConnectedMacs(TimeSpan.Zero);
        Assert.Empty(none);
    }

    private sealed class PushCountingManager(
        Mock<IDnsResolverService> dns,
        Mock<IDeviceControllerService> controller)
        : HeadlessDeviceManagerService(dns.Object, controller.Object)
    {
        public int RuntimeStatePushes { get; set; }

        protected override Task OnDeviceUpdatedAsync(AcDeviceState deviceState)
        {
            if (deviceState.RuntimeState is not null)
            {
                RuntimeStatePushes++;
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RefreshFirmwareAsync_DeviceQueryFails_ReturnsNullAndKeepsState()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");

        _mockDeviceController
            .Setup(x => x.GetDeviceFirmwareAsync(It.IsAny<GreeACLocalServer.Device.Requests.GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GreeACLocalServer.Device.Results.DeviceFirmwareResult(false, "NO_RESPONSE", "NO_RESPONSE"));

        var result = await _deviceManagerService.RefreshFirmwareAsync(mac);

        Assert.Null(result);
        var reloaded = await _deviceManagerService.GetAsync(mac);
        Assert.Null(reloaded!.FirmwareVersion);
    }

    // ---- WP-14: opportunistic firmware refresh throttle / dedup / gate ----

    private sealed class OptionsMonitorStub<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private HeadlessDeviceManagerService CreateWithFirmwareOptions(FirmwareUpdateOptions options) =>
        new(_mockDnsResolver.Object, _mockDeviceController.Object, firmwareUpdateService: null,
            new OptionsMonitorStub<FirmwareUpdateOptions>(options));

    private static Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000) =>
        WaitUntilAsync(() => Task.FromResult(condition()), timeoutMs);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }
            await Task.Delay(20);
        }
    }

    private void SetupFirmware(DeviceFirmwareResult result) =>
        _mockDeviceController
            .Setup(x => x.GetDeviceFirmwareAsync(It.IsAny<GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private int FirmwareCallCount() =>
        _mockDeviceController.Invocations.Count(i => i.Method.Name == nameof(IDeviceControllerService.GetDeviceFirmwareAsync));

    [Fact]
    public async Task OpportunisticRefresh_FailedQuery_IsNotRetriedOnNextReconnect()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        SetupFirmware(new DeviceFirmwareResult(false, "NO_RESPONSE", "NO_RESPONSE"));

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        await WaitUntilAsync(() => FirmwareCallCount() >= 1);
        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        await Task.Delay(200);

        Assert.Equal(1, FirmwareCallCount());
    }

    [Fact]
    public async Task OpportunisticRefresh_SuccessfulQuery_IsNotRepeatedOnNextReconnect()
    {
        var mac = "AA:BB:CC:DD:EE:FF";
        SetupFirmware(new DeviceFirmwareResult(true, "", hid: "362001065736+U-QCOM4004CV3.76.bin",
            firmwareVersion: "3.76", firmwareCode: "362001065736", macAddress: mac));

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        await WaitUntilAsync(() => FirmwareCallCount() >= 1);
        await WaitUntilAsync(async () => (await _deviceManagerService.GetAsync(mac))?.FirmwareVersion == "3.76");

        await _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100");
        await Task.Delay(200);

        Assert.Equal(1, FirmwareCallCount());
    }

    [Fact]
    public async Task OpportunisticRefresh_ConcurrentReconnects_QueryDeviceOnce()
    {
        var mac = "AA:BB:CC:DD:EE:FF";

        // Hold the single query in flight until the assertions are done, instead of
        // racing a fixed sleep against a background Task.Run on a busy CI thread pool.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mockDeviceController
            .Setup(x => x.GetDeviceFirmwareAsync(It.IsAny<GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await release.Task;
                return new DeviceFirmwareResult(false, "NO_RESPONSE", "NO_RESPONSE");
            });

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _deviceManagerService.UpdateOrAddAsync(mac, "192.168.1.100")));

        // One opportunistic refresh reached the controller; the other seven
        // reconnects were deduplicated while it was in flight.
        await WaitUntilAsync(() => FirmwareCallCount() >= 1);
        await Task.Delay(50);
        Assert.Equal(1, FirmwareCallCount());

        release.SetResult();
    }

    [Fact]
    public async Task AutoQueryDisabled_DoesNotQueryFirmwareOnReconnect()
    {
        var service = CreateWithFirmwareOptions(new FirmwareUpdateOptions { AutoQuery = false });
        SetupFirmware(new DeviceFirmwareResult(true, "", hid: "362001065736+U-QCOM4004CV3.76.bin",
            firmwareVersion: "3.76", firmwareCode: "362001065736"));

        await service.UpdateOrAddAsync("AA:BB:CC:DD:EE:FF", "192.168.1.100");
        await Task.Delay(200);

        Assert.Equal(0, FirmwareCallCount());
    }

    [Fact]
    public async Task AutoQueryDisabled_ManualRefreshStillWorks()
    {
        var service = CreateWithFirmwareOptions(new FirmwareUpdateOptions { AutoQuery = false });
        SetupFirmware(new DeviceFirmwareResult(true, "", hid: "362001065736+U-QCOM4004CV3.76.bin",
            firmwareVersion: "3.76", firmwareCode: "362001065736", macAddress: "AA:BB:CC:DD:EE:FF"));

        await service.UpdateOrAddAsync("AA:BB:CC:DD:EE:FF", "192.168.1.100");
        var refreshed = await service.RefreshFirmwareAsync("AA:BB:CC:DD:EE:FF");

        Assert.NotNull(refreshed);
        Assert.Equal("3.76", refreshed!.FirmwareVersion);
    }

    [Fact]
    public async Task GetAllDeviceStatesAsync_DoesNotTriggerCloudUpdateFetch()
    {
        var firmware = new Mock<IFirmwareUpdateService>();
        var service = new HeadlessDeviceManagerService(_mockDnsResolver.Object, _mockDeviceController.Object, firmware.Object);
        SetupFirmware(new DeviceFirmwareResult(true, "", hid: "362001065736+U-QCOM4004CV3.76.bin",
            firmwareVersion: "3.76", firmwareCode: "362001065736"));

        await service.UpdateOrAddAsync("AA:BB:CC:DD:EE:FF", "192.168.1.100");
        await service.RefreshFirmwareAsync("AA:BB:CC:DD:EE:FF");

        firmware.Invocations.Clear();
        _ = await service.GetAllDeviceStatesAsync();

        firmware.Verify(
            x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Never);
        firmware.Verify(
            x => x.CheckAsync(It.IsAny<string>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private sealed class OrderRecordingManager(
        Mock<IDnsResolverService> dns,
        Mock<IDeviceControllerService> controller,
        RecordingFirmwareService firmware)
        : HeadlessDeviceManagerService(dns.Object, controller.Object, firmware,
            new OptionsMonitorStub<FirmwareUpdateOptions>(new FirmwareUpdateOptions { AutoQuery = false, Enabled = true }))
    {
        private readonly RecordingFirmwareService _firmware = firmware;

        /// <summary>CheckAsync call count observed at each device-updated push.</summary>
        public List<int> ChecksBeforeEachPush { get; } = [];

        protected override Task OnDeviceUpdatedAsync(AcDeviceState deviceState)
        {
            ChecksBeforeEachPush.Add(_firmware.Calls.Count);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingFirmwareService : IFirmwareUpdateService
    {
        public List<bool> Calls { get; } = [];

        public Task<FirmwareUpdateInfo?> CheckAsync(string firmwareCode, string currentVersion, bool allowRemoteFetch = true, CancellationToken cancellationToken = default)
        {
            Calls.Add(allowRemoteFetch);
            return Task.FromResult<FirmwareUpdateInfo?>(new FirmwareUpdateInfo("9.9", true, false));
        }
    }

    [Fact]
    public async Task RefreshFirmwareAsync_WarmsCloudCacheBeforeThePush()
    {
        var firmware = new RecordingFirmwareService();
        var manager = new OrderRecordingManager(_mockDnsResolver, _mockDeviceController, firmware);
        SetupFirmware(new DeviceFirmwareResult(true, "", hid: "362001065736+U-QCOM4004CV3.76.bin",
            firmwareVersion: "3.76", firmwareCode: "362001065736", macAddress: "AA:BB:CC:DD:EE:FF"));

        await manager.UpdateOrAddAsync("AA:BB:CC:DD:EE:FF", "192.168.1.100");
        var dto = await manager.RefreshFirmwareAsync("AA:BB:CC:DD:EE:FF");

        // The remote (allowRemoteFetch: true) lookup ran first...
        Assert.True(firmware.Calls[0]);
        // ...and by the time the refresh's push fired, that lookup was already done.
        Assert.Equal(1, manager.ChecksBeforeEachPush[^1]);
        Assert.True(dto!.UpdateAvailable);
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithExistingDevice_RemovesDevice()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";

        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);

        // Verify device exists
        var deviceBefore = await _deviceManagerService.GetAsync(macAddress);
        Assert.NotNull(deviceBefore);

        // Act
        var removed = await _deviceManagerService.RemoveDeviceAsync(macAddress);

        // Assert
        Assert.True(removed);
        var deviceAfter = await _deviceManagerService.GetAsync(macAddress);
        Assert.Null(deviceAfter);
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithNonExistentDevice_ReturnsFalse()
    {
        // Arrange
        var macAddress = "AA:BB:CC:DD:EE:FF";

        // Act
        var removed = await _deviceManagerService.RemoveDeviceAsync(macAddress);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveDeviceAsync_WithEmptyMacAddress_ReturnsFalse()
    {
        // Act
        var removedEmpty = await _deviceManagerService.RemoveDeviceAsync("");
        var removedNull = await _deviceManagerService.RemoveDeviceAsync(null!);

        // Assert
        Assert.False(removedEmpty);
        Assert.False(removedNull);
    }
}
