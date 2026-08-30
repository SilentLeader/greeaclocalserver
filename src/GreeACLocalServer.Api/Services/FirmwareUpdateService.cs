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

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptionsMonitor<FirmwareUpdateOptions> _options = options;
    private readonly ILogger<FirmwareUpdateService> _logger = logger;

    public async Task<FirmwareUpdateInfo?> CheckAsync(string firmwareCode, string currentVersion, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(firmwareCode))
        {
            return null;
        }

        var ttl = TimeSpan.FromHours(opts.CacheHours > 0 ? opts.CacheHours : 24);
        if (_cache.TryGetValue(firmwareCode, out var cached) && DateTimeOffset.UtcNow - cached.FetchedAt < ttl)
        {
            return Project(cached.LatestVersion, cached.ForcedUpgrade, currentVersion);
        }

        var latest = await FetchLatestAsync(opts.BaseUrl, firmwareCode, cancellationToken);
        if (latest is null)
        {
            return null;
        }

        _cache[firmwareCode] = new CacheEntry(DateTimeOffset.UtcNow, latest.Value.version, latest.Value.forced);
        return Project(latest.Value.version, latest.Value.forced, currentVersion);
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

    private readonly record struct CacheEntry(DateTimeOffset FetchedAt, string LatestVersion, bool ForcedUpgrade);

    private sealed class LastVersionResponse
    {
        [JsonPropertyName("ver")]
        public string? Ver { get; set; }

        [JsonPropertyName("forcedUpgrade")]
        public int ForcedUpgrade { get; set; }
    }
}
