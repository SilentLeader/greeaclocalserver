using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Device.Interfaces;

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
