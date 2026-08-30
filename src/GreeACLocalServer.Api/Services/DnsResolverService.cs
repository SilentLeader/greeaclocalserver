using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace GreeACLocalServer.Api.Services;

public interface IDnsResolverService
{
    Task<string> ResolveDnsNameAsync(string ipAddress);
}

public class DnsResolverService : IDnsResolverService
{
    private static readonly TimeSpan SuccessTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(1);

    private readonly ILogger<DnsResolverService> _logger;
    private readonly IMemoryCache _cache;
    private readonly Func<string, Task<string?>> _reverseLookup;

    public DnsResolverService(ILogger<DnsResolverService> logger, IMemoryCache cache)
        : this(logger, cache, reverseLookup: null)
    {
    }

    /// <summary>
    /// Test/extensibility constructor: allows injecting the underlying reverse-DNS lookup.
    /// </summary>
    internal DnsResolverService(
        ILogger<DnsResolverService> logger,
        IMemoryCache cache,
        Func<string, Task<string?>>? reverseLookup)
    {
        _logger = logger;
        _cache = cache;
        _reverseLookup = reverseLookup ?? DefaultReverseLookupAsync;
    }

    public async Task<string> ResolveDnsNameAsync(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out _))
        {
            _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
            return ipAddress;
        }

        var cacheKey = "dns:" + ipAddress;
        if (_cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        string? resolved = null;
        try
        {
            resolved = await _reverseLookup(ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to resolve DNS name for {IpAddress}: {Error}", ipAddress, ex.Message);
        }

        var success = !string.IsNullOrEmpty(resolved) &&
                      !string.Equals(resolved, ipAddress, StringComparison.OrdinalIgnoreCase);
        var value = success ? resolved! : ipAddress;

        _cache.Set(cacheKey, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = success ? SuccessTtl : FailureTtl,
            Size = 1
        });

        if (success)
        {
            _logger.LogDebug("Resolved DNS name for {IpAddress}: {DnsName}", ipAddress, value);
        }

        return value;
    }

    private static async Task<string?> DefaultReverseLookupAsync(string ipAddress)
    {
        var hostEntry = await Dns.GetHostEntryAsync(IPAddress.Parse(ipAddress));
        return hostEntry.HostName;
    }
}
