using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GreeACLocalServer.Api.Tests;

public class ConfigServiceTests
{
    private readonly Mock<IOptionsMonitor<ServerOptions>> _mockServerOptions;
    private readonly Mock<IOptionsMonitor<DeviceManagerOptions>> _mockDeviceManagerOptions;
    private readonly ConfigService _configService;

    public ConfigServiceTests()
    {
        _mockServerOptions = new Mock<IOptionsMonitor<ServerOptions>>();
        _mockDeviceManagerOptions = new Mock<IOptionsMonitor<DeviceManagerOptions>>();
        _mockDeviceManagerOptions.Setup(x => x.CurrentValue).Returns(new DeviceManagerOptions());
        _configService = new ConfigService(_mockServerOptions.Object, _mockDeviceManagerOptions.Object);
    }

    [Fact]
    public async Task GetServerConfigAsync_ReturnsCorrectConfig()
    {
        // Arrange
        var serverOptions = new ServerOptions
        {
            EnableManagement = true,
            EnableUI = false
        };
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(serverOptions);

        // Act
        var result = await _configService.GetServerConfigAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EnableManagement);
        Assert.False(result.EnableUI);
    }

    [Fact]
    public async Task GetServerConfigAsync_WithManagementDisabled_ReturnsCorrectConfig()
    {
        // Arrange
        var serverOptions = new ServerOptions
        {
            EnableManagement = false,
            EnableUI = true
        };
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(serverOptions);

        // Act
        var result = await _configService.GetServerConfigAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.EnableManagement);
        Assert.True(result.EnableUI);
    }

    [Fact]
    public async Task GetServerConfigAsync_IncludesDeviceTimeoutMinutesFromDeviceManagerOptions()
    {
        // Arrange
        _mockServerOptions.Setup(x => x.CurrentValue).Returns(new ServerOptions());
        _mockDeviceManagerOptions.Setup(x => x.CurrentValue)
            .Returns(new DeviceManagerOptions { DeviceTimeoutMinutes = 42 });

        // Act
        var result = await _configService.GetServerConfigAsync(CancellationToken.None);

        // Assert
        Assert.Equal(42, result.DeviceTimeoutMinutes);
    }
}
