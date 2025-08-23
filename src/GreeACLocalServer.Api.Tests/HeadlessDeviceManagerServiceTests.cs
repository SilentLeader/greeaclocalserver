using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;

namespace GreeACLocalServer.Api.Tests;

public class HeadlessDeviceManagerServiceTests
{
    private readonly Mock<IOptions<DeviceManagerOptions>> _mockOptions;
    private readonly Mock<IDnsResolverService> _mockDnsResolver;
    private readonly HeadlessDeviceManagerService _deviceManagerService;
    private readonly DeviceManagerOptions _deviceManagerOptions;

    public HeadlessDeviceManagerServiceTests()
    {
        _deviceManagerOptions = new DeviceManagerOptions
        {
            DeviceTimeoutMinutes = 30
        };

        _mockOptions = new Mock<IOptions<DeviceManagerOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(_deviceManagerOptions);

        _mockDnsResolver = new Mock<IDnsResolverService>();
        _mockDnsResolver.Setup(x => x.ResolveDnsNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string ip) => $"device-{ip.Replace(".", "-")}.local");

        _deviceManagerService = new HeadlessDeviceManagerService(_mockOptions.Object, _mockDnsResolver.Object);
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
    public async Task RemoveStaleDevicesAsync_WithExpiredDevices_RemovesDevices()
    {
        // Arrange
        _deviceManagerOptions.DeviceTimeoutMinutes = 0; // Immediate timeout for testing
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";

        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);
        
        // Wait a bit to ensure timeout
        await Task.Delay(100);

        // Act
        await _deviceManagerService.RemoveStaleDevicesAsync();

        // Assert
        var device = await _deviceManagerService.GetAsync(macAddress);
        Assert.Null(device);
    }

    [Fact]
    public async Task RemoveStaleDevicesAsync_WithRecentDevices_DoesNotRemoveDevices()
    {
        // Arrange
        _deviceManagerOptions.DeviceTimeoutMinutes = 60; // Long timeout
        var macAddress = "AA:BB:CC:DD:EE:FF";
        var ipAddress = "192.168.1.100";

        await _deviceManagerService.UpdateOrAddAsync(macAddress, ipAddress);

        // Act
        await _deviceManagerService.RemoveStaleDevicesAsync();

        // Assert
        var device = await _deviceManagerService.GetAsync(macAddress);
        Assert.NotNull(device);
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
    public void Constructor_WithNullOptions_ThrowsNullReferenceException()
    {
        // Arrange, Act & Assert
        Assert.Throws<NullReferenceException>(() => 
            new HeadlessDeviceManagerService(null!, _mockDnsResolver.Object));
    }

    [Fact]
    public void Constructor_WithNullDnsResolver_DoesNotThrowImmediately()
    {
        // Arrange, Act & Assert - Null DNS resolver doesn't fail immediately in constructor
        var service = new HeadlessDeviceManagerService(_mockOptions.Object, null!);
        Assert.NotNull(service);
        
        // But would fail when actually using DNS resolution
        // We don't test that here as it would require async testing
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
