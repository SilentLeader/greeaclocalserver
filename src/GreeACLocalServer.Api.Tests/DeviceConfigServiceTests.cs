using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Results;
using GreeACLocalServer.Shared.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GreeACLocalServer.Api.Tests;

/// <summary>
/// Guards WP-06 finding F12: every device-config operation (query included) must be
/// gated behind <see cref="ServerOptions.EnableManagement"/>, since all of them poke
/// the device over UDP.
/// </summary>
public class DeviceConfigServiceTests
{
    private readonly Mock<IDeviceControllerService> _controller = new(MockBehavior.Strict);
    private readonly Mock<IOptionsMonitor<ServerOptions>> _options = new();

    private DeviceConfigService CreateSut(bool enableManagement)
    {
        _options.Setup(x => x.CurrentValue).Returns(new ServerOptions { EnableManagement = enableManagement });
        return new DeviceConfigService(NullLogger<DeviceConfigService>.Instance, _controller.Object, _options.Object);
    }

    [Fact]
    public async Task QueryDeviceStatusAsync_ManagementDisabled_RejectsWithoutTouchingDevice()
    {
        var sut = CreateSut(enableManagement: false);

        var response = await sut.QueryDeviceStatusAsync(new QueryDeviceStatusRequest { IpAddress = "192.168.1.10" });

        Assert.False(response.Success);
        Assert.Equal("MANAGEMENT_DISABLED", response.ErrorCode);
        _controller.Verify(x => x.GetDeviceStatusAsync(It.IsAny<GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDeviceNameAsync_ManagementDisabled_RejectsWithoutTouchingDevice()
    {
        var sut = CreateSut(enableManagement: false);

        var response = await sut.SetDeviceNameAsync(new UpdateDeviceNameRequest { IpAddress = "192.168.1.10", DeviceName = "Nappali" });

        Assert.False(response.Success);
        Assert.Equal("MANAGEMENT_DISABLED", response.ErrorCode);
        _controller.Verify(x => x.SetDeviceNameAsync(It.IsAny<SetDeviceNameRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetRemoteHostAsync_ManagementDisabled_RejectsWithoutTouchingDevice()
    {
        var sut = CreateSut(enableManagement: false);

        var response = await sut.SetRemoteHostAsync(new UpdateRemoteHostRequest { IpAddress = "192.168.1.10", RemoteHost = "example.org" });

        Assert.False(response.Success);
        Assert.Equal("MANAGEMENT_DISABLED", response.ErrorCode);
        _controller.Verify(x => x.SetRemoteHostAsync(It.IsAny<SetRemoteHostRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task QueryDeviceStatusAsync_ManagementEnabled_ForwardsToController()
    {
        var sut = CreateSut(enableManagement: true);
        _controller
            .Setup(x => x.GetDeviceStatusAsync(It.IsAny<GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceStatusResult(true, string.Empty, deviceName: "Nappali", remoteHost: "h", macAddress: "AABB"));

        var response = await sut.QueryDeviceStatusAsync(new QueryDeviceStatusRequest { IpAddress = "192.168.1.10" });

        Assert.True(response.Success);
        Assert.Equal("Nappali", response.DeviceName);
        _controller.Verify(x => x.GetDeviceStatusAsync(It.IsAny<GetDeviceStatusRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
