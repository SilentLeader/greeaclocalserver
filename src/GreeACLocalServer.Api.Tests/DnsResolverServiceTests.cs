using Xunit;
using Microsoft.Extensions.Logging;
using GreeACLocalServer.Api.Services;
using Moq;

namespace GreeACLocalServer.Api.Tests;

public class DnsResolverServiceTests
{
    private DnsResolverService CreateService()
    {
        var logger = Mock.Of<ILogger<DnsResolverService>>();
        return new DnsResolverService(logger);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsHostnameForValidIp()
    {
        // Arrange
        var service = CreateService();
        
        // Act - Use localhost which should always resolve
        var result = await service.ResolveDnsNameAsync("127.0.0.1");
        
        // Assert - Should return either a hostname or fall back to IP
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Result should be either a hostname or the IP address itself
        Assert.True(result == "127.0.0.1" || result.Contains("localhost") || result.Contains('.'));
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsIpForInvalidIp()
    {
        // Arrange
        var service = CreateService();
        
        // Act
        var result = await service.ResolveDnsNameAsync("invalid-ip");
        
        // Assert
        Assert.Equal("invalid-ip", result);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsIpForUnresolvableIp()
    {
        // Arrange
        var service = CreateService();
        
        // Act - Use a private IP that likely won't resolve
        var result = await service.ResolveDnsNameAsync("192.168.255.254");
        
        // Assert - Should fall back to IP address
        Assert.Equal("192.168.255.254", result);
    }
}
