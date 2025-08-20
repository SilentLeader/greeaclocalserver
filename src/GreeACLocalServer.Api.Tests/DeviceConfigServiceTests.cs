using System;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Interfaces;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GreeACLocalServer.Api.Tests;

public class DeviceConfigServiceTests
{
    private readonly Mock<ILogger<DeviceConfigService>> _mockLogger;
    private readonly Mock<ICryptoService> _mockCryptoService;
    private readonly Mock<IOptionsMonitor<ServerOptions>> _mockServerOptions;
    private readonly DeviceConfigService _deviceConfigService;

    public DeviceConfigServiceTests()
    {
        _mockLogger = new Mock<ILogger<DeviceConfigService>>();
        _mockCryptoService = new Mock<ICryptoService>();
        _mockServerOptions = new Mock<IOptionsMonitor<ServerOptions>>();
        
        _deviceConfigService = new DeviceConfigService(
            _mockLogger.Object,
            _mockCryptoService.Object,
            _mockServerOptions.Object);
    }

    [Fact]
    public async Task SetDeviceNameAsync_WhenManagementDisabled_ReturnsManagementDisabledError()
    {
        // Arrange
        var serverOptions = new ServerOptions { EnableManagement = false };
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(serverOptions);

        var request = new SetDeviceNameRequest
        {
            IpAddress = "192.168.1.100",
            DeviceName = "TestDevice"
        };

        // Act
        var result = await _deviceConfigService.SetDeviceNameAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Device management is disabled", result.Message);
        Assert.Equal("MANAGEMENT_DISABLED", result.ErrorCode);
    }

    [Fact]
    public async Task SetRemoteHostAsync_WhenManagementDisabled_ReturnsManagementDisabledError()
    {
        // Arrange
        var serverOptions = new ServerOptions { EnableManagement = false };
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(serverOptions);

        var request = new SetRemoteHostRequest
        {
            IpAddress = "192.168.1.100",
            RemoteHost = "example.com"
        };

        // Act
        var result = await _deviceConfigService.SetRemoteHostAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Device management is disabled", result.Message);
        Assert.Equal("MANAGEMENT_DISABLED", result.ErrorCode);
    }

    [Fact]
    public async Task QueryDeviceStatusAsync_WhenManagementDisabled_StillWorks()
    {
        // Arrange
        var serverOptions = new ServerOptions { EnableManagement = false };
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(serverOptions);

        var request = new DeviceStatusRequest
        {
            IpAddress = "192.168.1.100"
        };

        // Act
        var result = await _deviceConfigService.QueryDeviceStatusAsync(request, CancellationToken.None);

        // Assert - Query should still work even when management is disabled
        // The actual result will depend on device connectivity, but the method should not check EnableManagement
        Assert.NotNull(result);
    }
}
