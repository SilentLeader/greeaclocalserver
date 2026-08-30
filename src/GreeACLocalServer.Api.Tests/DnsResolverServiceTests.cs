using Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GreeACLocalServer.Api.Services;
using Moq;

namespace GreeACLocalServer.Api.Tests;

public class DnsResolverServiceTests
{
    private static DnsResolverService CreateService(Func<string, Task<string?>>? reverseLookup = null)
    {
        var logger = Mock.Of<ILogger<DnsResolverService>>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new DnsResolverService(logger, cache, reverseLookup);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsHostnameForValidIp()
    {
        var service = CreateService(_ => Task.FromResult<string?>("host.example.local"));

        var result = await service.ResolveDnsNameAsync("192.168.1.10");

        Assert.Equal("host.example.local", result);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsIpForInvalidIp()
    {
        var service = CreateService();

        var result = await service.ResolveDnsNameAsync("invalid-ip");

        Assert.Equal("invalid-ip", result);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_ReturnsIpWhenLookupFails()
    {
        var service = CreateService(_ => throw new InvalidOperationException("boom"));

        var result = await service.ResolveDnsNameAsync("192.168.255.254");

        Assert.Equal("192.168.255.254", result);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_CachesSuccessfulLookup()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            return Task.FromResult<string?>("host.example.local");
        });

        var first = await service.ResolveDnsNameAsync("192.168.1.10");
        var second = await service.ResolveDnsNameAsync("192.168.1.10");

        Assert.Equal("host.example.local", first);
        Assert.Equal("host.example.local", second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ResolveDnsNameAsync_CachesFailedLookup()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            return Task.FromResult<string?>(null);
        });

        await service.ResolveDnsNameAsync("192.168.1.10");
        await service.ResolveDnsNameAsync("192.168.1.10");

        Assert.Equal(1, calls);
    }
}
