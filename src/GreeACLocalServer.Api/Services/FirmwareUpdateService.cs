using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreeACLocalServer.Device.Services;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Looks up the latest published firmware for a given firmware code on the GREE
/// update server. Opt-in (<see cref="FirmwareUpdateOptions.Enabled"/>) and cached
/// per firmware code, since GREE firmware releases are months apart.
/// </summary>
public class FirmwareUpdateService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<FirmwareUpdateOptions> options,
    ILogger<FirmwareUpdateService> logger) : IFirmwareUpdateService
{
    internal const string HttpClientName = "gree-firmware";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    /// <summary>Shared in-progress fetches, keyed by firmware code, so a cold-cache burst hits the network once.</summary>
    private readonly ConcurrentDictionary<string, Lazy<Task<(string version, bool forced)?>>> _inFlight = new();

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<FirmwareUpdateOptions> _options = options;
    private readonly ILogger<FirmwareUpdateService> _logger = logger;

    private int _startupLogged;

    private void LogStartupOnce(FirmwareUpdateOptions opts)
    {
        if (opts.Enabled && Interlocked.Exchange(ref _startupLogged, 1) == 0)
        {
            _logger.LogInformation(
                "Firmware cloud update check is ENABLED: firmware codes will be sent to {BaseUrl}",
                opts.BaseUrl);
        }
    }

    public async Task<FirmwareUpdateInfo?> CheckAsync(string firmwareCode, string currentVersion, bool allowRemoteFetch = true, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        LogStartupOnce(opts);
        if (!opts.Enabled || string.IsNullOrWhiteSpace(firmwareCode))
        {
            return null;
        }

        var ttl = TimeSpan.FromHours(opts.CacheHours > 0 ? opts.CacheHours : 24);
        var hasCache = _cache.TryGetValue(firmwareCode, out var cached);

        if (hasCache && (!allowRemoteFetch || DateTimeOffset.UtcNow - cached.FetchedAt < ttl))
        {
            return Project(cached.LatestVersion, cached.ForcedUpgrade, currentVersion);
        }

        if (!allowRemoteFetch)
        {
            // Cache-only mode: a full miss stays null; the opportunistic refresh
            // and the next SignalR upsert bring the data in once the cache warms.
            return null;
        }

        var latest = await FetchLatestSharedAsync(opts.BaseUrl, firmwareCode, cancellationToken);
        if (latest is null)
        {
            return null;
        }

        _cache[firmwareCode] = new CacheEntry(DateTimeOffset.UtcNow, latest.Value.version, latest.Value.forced);
        return Project(latest.Value.version, latest.Value.forced, currentVersion);
    }

    /// <summary>
    /// Coalesces concurrent lookups for the same firmware code onto a single
    /// <see cref="FetchLatestAsync"/> call. The entry is removed once complete so
    /// a later refresh starts fresh.
    /// </summary>
    private async Task<(string version, bool forced)?> FetchLatestSharedAsync(string baseUrl, string firmwareCode, CancellationToken cancellationToken)
    {
        var lazy = _inFlight.GetOrAdd(
            firmwareCode,
            key => new Lazy<Task<(string version, bool forced)?>>(
                () => FetchLatestAsync(baseUrl, key, CancellationToken.None)));

        try
        {
            return await lazy.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompleted)
            {
                _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<(string version, bool forced)?>>>(firmwareCode, lazy));
            }
        }
    }

    private static FirmwareUpdateInfo Project(string latestVersion, bool forced, string currentVersion) =>
        new(latestVersion, FirmwareInfo.CompareVersions(latestVersion, currentVersion) > 0, forced);

    private async Task<(string version, bool forced)?> FetchLatestAsync(string baseUrl, string firmwareCode, CancellationToken cancellationToken)
    {
        try
        {
            var separator = baseUrl.Contains('?') ? '&' : '?';
            var url = $"{baseUrl}{separator}firmwareCode={Uri.EscapeDataString(firmwareCode)}";

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<LastVersionResponse>(stream, _jsonOptions, cancellationToken);

            if (payload is null || string.IsNullOrWhiteSpace(payload.Ver))
            {
                _logger.LogDebug("Firmware lookup for {FirmwareCode} returned no version", firmwareCode);
                return null;
            }

            return (payload.Ver.Trim(), payload.ForcedUpgrade != 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogDebug(ex, "Firmware lookup for {FirmwareCode} failed", firmwareCode);
            return null;
        }
    }

    /// <summary>Test seam: pre-populate the per-code cache with an entry of a chosen age.</summary>
    internal void SeedCacheEntryForTests(string firmwareCode, string latestVersion, bool forcedUpgrade, DateTimeOffset fetchedAt)
        => _cache[firmwareCode] = new CacheEntry(fetchedAt, latestVersion, forcedUpgrade);

    private readonly record struct CacheEntry(DateTimeOffset FetchedAt, string LatestVersion, bool ForcedUpgrade);

    private sealed class LastVersionResponse
    {
        [JsonPropertyName("ver")]
        public string? Ver { get; set; }

        [JsonPropertyName("forcedUpgrade")]
        public int ForcedUpgrade { get; set; }
    }
}
