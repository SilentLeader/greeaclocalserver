using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;

namespace GreeACLocalServer.Api.Tests;

public class FirmwareUpdateServiceTests
{
    private sealed class StubHandler(string json, HttpStatusCode status = HttpStatusCode.OK, TimeSpan delay = default) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static FirmwareUpdateService Create(HttpMessageHandler handler, FirmwareUpdateOptions options) =>
        new(new SingleClientFactory(handler),
            new StaticOptionsMonitor<FirmwareUpdateOptions>(options),
            NullLogger<FirmwareUpdateService>.Instance);

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    [Fact]
    public async Task CheckAsync_Disabled_ReturnsNullWithoutHttpCall()
    {
        var handler = new StubHandler("{\"ver\":\"3.77\"}");
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = false });

        var result = await service.CheckAsync("362001065736", "3.76");

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CheckAsync_NewerVersion_ReportsUpdateAvailable()
    {
        var handler = new StubHandler("{\"ver\":\"3.77\",\"forcedUpgrade\":0}");
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true });

        var result = await service.CheckAsync("362001065736", "3.76");

        Assert.NotNull(result);
        Assert.Equal("3.77", result!.LatestVersion);
        Assert.True(result.UpdateAvailable);
        Assert.False(result.ForcedUpgrade);
    }

    [Fact]
    public async Task CheckAsync_SameVersion_NoUpdateAndResultIsCached()
    {
        var handler = new StubHandler("{\"ver\":\"3.76\"}");
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true, CacheHours = 24 });

        var first = await service.CheckAsync("362001065736", "3.76");
        var second = await service.CheckAsync("362001065736", "3.76");

        Assert.False(first!.UpdateAvailable);
        Assert.False(second!.UpdateAvailable);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CheckAsync_CacheOnly_EmptyCache_ReturnsNullWithoutHttp()
    {
        var handler = new StubHandler("{\"ver\":\"3.77\"}");
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true });

        var result = await service.CheckAsync("362001065736", "3.76", allowRemoteFetch: false);

        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CheckAsync_CacheOnly_StaleEntry_ComputesFromStaleWithoutHttp()
    {
        var handler = new StubHandler("{\"ver\":\"9.99\"}");
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true, CacheHours = 1 });
        service.SeedCacheEntryForTests("362001065736", "3.77", forcedUpgrade: false, DateTimeOffset.UtcNow.AddDays(-30));

        var result = await service.CheckAsync("362001065736", "3.76", allowRemoteFetch: false);

        Assert.NotNull(result);
        Assert.Equal("3.77", result!.LatestVersion);
        Assert.True(result.UpdateAvailable);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CheckAsync_ConcurrentColdCache_HitsNetworkOnce()
    {
        var handler = new StubHandler("{\"ver\":\"3.77\"}", delay: TimeSpan.FromMilliseconds(150));
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true });

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => service.CheckAsync("362001065736", "3.76")));

        Assert.All(results, r => Assert.Equal("3.77", r!.LatestVersion));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task CheckAsync_HttpError_ReturnsNull()
    {
        var handler = new StubHandler("nope", HttpStatusCode.InternalServerError);
        var service = Create(handler, new FirmwareUpdateOptions { Enabled = true });

        var result = await service.CheckAsync("362001065736", "3.76");

        Assert.Null(result);
    }
}
