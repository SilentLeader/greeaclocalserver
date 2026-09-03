using System.Collections.Concurrent;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Requests;
using GreeACLocalServer.Device.Results;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Base device manager service that provides core functionality without SignalR dependencies.
/// Can be used directly for headless mode or inherited by DeviceManagerService for UI mode.
/// </summary>
public class HeadlessDeviceManagerService(
    IDnsResolverService dnsResolver,
    IDeviceControllerService deviceController,
    IFirmwareUpdateService? firmwareUpdateService = null,
    IOptionsMonitor<FirmwareUpdateOptions>? firmwareOptions = null) : IInternalDeviceManagerService

{
    /// <summary>After a successful opportunistic firmware query, do not re-query for this long.</summary>
    private static readonly TimeSpan FirmwareRefreshInterval = TimeSpan.FromDays(7);

    /// <summary>After a failed opportunistic firmware query, back off for this long before retrying.</summary>
    private static readonly TimeSpan FirmwareRetryInterval = TimeSpan.FromHours(6);

    protected readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();
    protected readonly IDnsResolverService _dnsResolver = dnsResolver;
    private readonly IDeviceControllerService _deviceController = deviceController;
    private readonly IFirmwareUpdateService? _firmwareUpdateService = firmwareUpdateService;
    private readonly IOptionsMonitor<FirmwareUpdateOptions>? _firmwareOptions = firmwareOptions;

    /// <summary>MACs with an opportunistic firmware query in progress, so reconnect bursts don't pile up.</summary>
    private readonly ConcurrentDictionary<string, byte> _firmwareRefreshInFlight = new();

    public virtual async Task UpdateOrAddAsync(string macAddress, string? ipAddress, int port = 0, bool isTls = false)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        var dnsName = await _dnsResolver.ResolveDnsNameAsync(ipAddress);
        var now = DateTime.UtcNow;

        var state = _deviceStates.AddOrUpdate(macAddress,
            key => new AcDeviceState
            {
                MacAddress = macAddress,
                IpAddress = ipAddress,
                DNSName = dnsName,
                LastConnectionTime = now,
                Endpoints = MergeEndpoint([], port, isTls, now)
            },
            (key, existing) => existing with
            {
                IpAddress = ipAddress,
                DNSName = dnsName,
                LastConnectionTime = now,
                Endpoints = MergeEndpoint(existing.Endpoints, port, isTls, now),
                // A reconnect gives a device that was backed off / given up on a fresh chance.
                RuntimeStatePollFailures = 0
            });

        // Virtual method hook for derived classes (e.g., SignalR notifications)
        await OnDeviceUpdatedAsync(state);

        MaybeRefreshFirmwareInBackground(state);
    }

    public virtual async Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default)
    {
        // Cache-only: a device-list poll must not fan out into N cloud lookups.
        var projected = await Task.WhenAll(
            _deviceStates.Values.Select(s => ProjectAsync(s, allowRemoteFetch: false, cancellationToken)));
        return projected;
    }

    public virtual async Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (_deviceStates.TryGetValue(macAddress, out var state))
        {
            return await ProjectAsync(state, allowRemoteFetch: true, cancellationToken);
        }
        return null;
    }

    public virtual async Task<DeviceDto?> RefreshFirmwareAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(macAddress) || !_deviceStates.TryGetValue(macAddress, out var current))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(current.IpAddress))
        {
            return null;
        }

        DeviceFirmwareResult? result;
        try
        {
            result = await _deviceController.GetDeviceFirmwareAsync(new GetDeviceStatusRequest(current.IpAddress), cancellationToken);
        }
        catch
        {
            StampFirmwareAttempt(macAddress);
            throw;
        }

        var succeeded = result is not null && result.IsSuccess && !string.IsNullOrWhiteSpace(result.Hid);
        var updated = StampFirmwareResult(macAddress, succeeded ? result : null);

        if (!succeeded || updated is null)
        {
            return null;
        }

        // Project first (allowRemoteFetch: true) so the cloud lookup result is in
        // the cache before the SignalR push: the push re-projects cache-only, and
        // other UI clients then see the fresh UpdateAvailable in the same round
        // rather than one cycle later.
        var dto = await ProjectAsync(updated, allowRemoteFetch: true, cancellationToken);
        await OnDeviceUpdatedAsync(updated);
        return dto;
    }

    /// <summary>
    /// Re-queries the device's operating state over the local network. On success
    /// the new reading is stamped onto the device state and (when it actually
    /// changed) pushed to clients; on failure any previously known state is
    /// cleared and the clear is pushed once.
    /// </summary>
    public virtual async Task<DeviceDto?> RefreshRuntimeStateAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(macAddress) || !_deviceStates.TryGetValue(macAddress, out var current))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(current.IpAddress))
        {
            return null;
        }

        DeviceRuntimeStateResult? result = null;
        try
        {
            result = await _deviceController.GetDeviceRuntimeStateAsync(new GetDeviceStatusRequest(current.IpAddress), cancellationToken);
        }
        catch
        {
            // Treated the same as a failed query below: the state is cleared.
        }

        var reading = ToRuntimeState(result);

        var (updated, changed) = StampRuntimeState(macAddress, reading);
        if (updated is null)
        {
            return null;
        }

        if (changed)
        {
            await OnDeviceUpdatedAsync(updated);
        }

        return reading is null ? null : await ProjectAsync(updated, allowRemoteFetch: false, cancellationToken);
    }

    /// <summary>MAC addresses whose last connection was within <paramref name="window"/>.</summary>
    public IReadOnlyCollection<string> GetRecentlyConnectedMacs(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return _deviceStates.Values
            .Where(s => s.LastConnectionTime >= cutoff)
            .Select(s => s.MacAddress)
            .ToList();
    }

    private static AcRuntimeState? ToRuntimeState(DeviceRuntimeStateResult? result)
    {
        if (result is null || !result.IsSuccess
            || result.Power is not { } power
            || result.Mode is not { } mode
            || result.TargetTemperature is not { } setTemp)
        {
            return null;
        }

        return new AcRuntimeState(
            power,
            Enum.IsDefined(typeof(AcMode), mode) ? (AcMode)mode : AcMode.Unknown,
            setTemp,
            result.TemperatureUnit == 1 ? AcTemperatureUnit.Fahrenheit : AcTemperatureUnit.Celsius,
            DateTime.UtcNow,
            AdjustCurrentTemperature(result.CurrentTemperatureRaw));
    }

    /// <summary>
    /// Turns the raw <c>TemSen</c> reading into whole degrees Celsius: strips the
    /// +40 offset and rejects anything outside a plausible indoor range (devices
    /// without a sensor report 0 or a nonsense value).
    /// </summary>
    private static int? AdjustCurrentTemperature(int? raw)
    {
        if (raw is not { } value || value == 0)
        {
            return null;
        }

        var celsius = value - 40;
        return celsius is >= -40 and <= 60 ? celsius : null;
    }

    /// <summary>
    /// Writes <paramref name="reading"/> (or a clear when null) onto the device
    /// state via a compare-exchange loop that never resurrects a removed device.
    /// Returns the new state and whether clients need to be notified.
    /// </summary>
    private (AcDeviceState? State, bool Changed) StampRuntimeState(string macAddress, AcRuntimeState? reading)
    {
        var now = DateTime.UtcNow;
        while (_deviceStates.TryGetValue(macAddress, out var existing))
        {
            var previous = existing.RuntimeState;
            // "changed" drives the SignalR push and must only reflect a real reading
            // change — the attempt timestamp and failure counter never trigger a push.
            var changed = reading is null ? previous is not null : !reading.SameReadingAs(previous);
            var next = existing with
            {
                RuntimeState = reading,
                RuntimeStateAttemptedUtc = now,
                RuntimeStatePollFailures = reading is null ? existing.RuntimeStatePollFailures + 1 : 0
            };

            if (_deviceStates.TryUpdate(macAddress, next, existing))
            {
                return (next, changed);
            }
        }

        return (null, false);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetRuntimeStatePollTargets(
        TimeSpan window, TimeSpan failureBackoff, int maxConsecutiveFailures)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - window;
        return _deviceStates.Values
            .Where(s => s.LastConnectionTime >= cutoff)
            .Where(s => maxConsecutiveFailures <= 0 || s.RuntimeStatePollFailures < maxConsecutiveFailures)
            .Where(s => s.RuntimeStatePollFailures == 0
                        || s.RuntimeStateAttemptedUtc is not { } attempted
                        || now - attempted >= failureBackoff)
            .Select(s => s.MacAddress)
            .ToList();
    }

    /// <summary>
    /// Records that a firmware query was attempted now (throttles the opportunistic
    /// background refresh even when the device is unreachable).
    /// </summary>
    private void StampFirmwareAttempt(string macAddress) => StampFirmwareResult(macAddress, null);

    /// <summary>
    /// Marks a firmware query attempt on the device state. When
    /// <paramref name="result"/> is a successful lookup the parsed firmware fields
    /// and <see cref="AcDeviceState.FirmwareCheckedUtc"/> are updated too; either
    /// way <see cref="AcDeviceState.FirmwareRefreshAttemptedUtc"/> is set.
    /// </summary>
    private AcDeviceState? StampFirmwareResult(string macAddress, DeviceFirmwareResult? result)
    {
        var now = DateTime.UtcNow;

        AcDeviceState Apply(AcDeviceState state)
        {
            state = state with { FirmwareRefreshAttemptedUtc = now };
            if (result is null)
            {
                return state;
            }

            return state with
            {
                FirmwareHid = result.Hid,
                FirmwareVersion = string.IsNullOrWhiteSpace(result.FirmwareVersion) ? null : result.FirmwareVersion,
                FirmwareCode = string.IsNullOrWhiteSpace(result.FirmwareCode) ? null : result.FirmwareCode,
                FirmwareCheckedUtc = now
            };
        }

        // Compare-exchange loop: never resurrect a device that was removed concurrently.
        while (_deviceStates.TryGetValue(macAddress, out var existing))
        {
            var next = Apply(existing);
            if (_deviceStates.TryUpdate(macAddress, next, existing))
            {
                return next;
            }
        }

        return null;
    }

    /// <summary>
    /// Fire-and-forget firmware query for devices that have never reported one (or
    /// whose last successful query is stale). Gated by
    /// <see cref="FirmwareUpdateOptions.AutoQuery"/>, throttled with a two-tier
    /// backoff (7 days after success, 6 hours after failure) and deduplicated per
    /// MAC. Best effort: failures are swallowed, the previous value is kept.
    /// </summary>
    private void MaybeRefreshFirmwareInBackground(AcDeviceState state)
    {
        if (_firmwareOptions is not null && !_firmwareOptions.CurrentValue.AutoQuery)
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (state.FirmwareCheckedUtc is { } ok && now - ok < FirmwareRefreshInterval)
        {
            return;
        }
        if (state.FirmwareRefreshAttemptedUtc is { } tried && now - tried < FirmwareRetryInterval)
        {
            return;
        }
        if (!_firmwareRefreshInFlight.TryAdd(state.MacAddress, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshFirmwareAsync(state.MacAddress);
            }
            catch
            {
                // opportunistic only
            }
            finally
            {
                _firmwareRefreshInFlight.TryRemove(state.MacAddress, out _);
            }
        });
    }

    /// <summary>
    /// Projects the internal device state onto the wire contract, enriching it
    /// with a firmware update check when one is configured. The check is
    /// cache-backed (see <see cref="FirmwareUpdateService"/>) so repeated calls
    /// are cheap.
    /// </summary>
    protected async Task<DeviceDto> ProjectAsync(AcDeviceState state, bool allowRemoteFetch, CancellationToken cancellationToken = default)
    {
        var dto = ToDto(state);

        if (_firmwareUpdateService is null
            || string.IsNullOrWhiteSpace(state.FirmwareCode)
            || string.IsNullOrWhiteSpace(state.FirmwareVersion))
        {
            return dto;
        }

        var update = await _firmwareUpdateService.CheckAsync(state.FirmwareCode, state.FirmwareVersion, allowRemoteFetch, cancellationToken);
        if (update is null)
        {
            return dto;
        }

        return dto with
        {
            LatestFirmwareVersion = update.LatestVersion,
            UpdateAvailable = update.UpdateAvailable
        };
    }

    /// <summary>Projects the internal device state onto the wire contract (no external lookups).</summary>
    protected static DeviceDto ToDto(AcDeviceState state) => new(
        state.MacAddress,
        state.IpAddress,
        state.DNSName,
        state.LastConnectionTime,
        state.Endpoints.Select(e => new DeviceEndpointDto(e.Port, e.IsTls, e.LastSeenUtc)).ToList())
    {
        FirmwareVersion = state.FirmwareVersion,
        FirmwareCode = state.FirmwareCode,
        RuntimeState = state.RuntimeState is { } rs
            ? new AcRuntimeStateDto(rs.Power, rs.Mode, rs.TargetTemperature, rs.TemperatureUnit, rs.QueriedUtc, rs.CurrentTemperature)
            : null
    };

    /// <summary>
    /// Returns a new endpoint list with the observed (<paramref name="port"/>,
    /// <paramref name="isTls"/>) pair recorded: the matching entry's timestamp is
    /// refreshed, or a new entry is appended. Ports &lt;= 0 (unknown) are ignored.
    /// The result is ordered by port, then plaintext before TLS.
    /// </summary>
    private static IReadOnlyList<DeviceEndpoint> MergeEndpoint(
        IReadOnlyList<DeviceEndpoint> existing, int port, bool isTls, DateTime now)
    {
        if (port <= 0)
        {
            return existing;
        }

        var merged = existing
            .Where(e => e.Port != port || e.IsTls != isTls)
            .Append(new DeviceEndpoint(port, isTls, now))
            .OrderBy(e => e.Port)
            .ThenBy(e => e.IsTls)
            .ToList();

        return merged;
    }

    public virtual async Task<bool> RemoveDeviceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return false;
        }

        if (_deviceStates.TryRemove(macAddress, out _))
        {
            // Notify derived classes (e.g., SignalR notifications)
            await OnDevicesRemovedAsync(new List<string> { macAddress });
            return true;
        }
        return false;
    }

    public virtual async Task<bool> RemoveDeviceAsync(string macAddress)
    {
        return await RemoveDeviceAsync(macAddress, CancellationToken.None);
    }

    /// <summary>
    /// Virtual method called when a device is updated or added.
    /// Override in derived classes to add additional functionality (e.g., SignalR notifications).
    /// </summary>
    /// <param name="deviceState">The updated device state</param>
    protected virtual async Task OnDeviceUpdatedAsync(AcDeviceState deviceState)
    {
        // Base implementation does nothing - override in derived classes
        await Task.CompletedTask;
    }

    /// <summary>
    /// Virtual method called when devices are removed due to timeout.
    /// Override in derived classes to add additional functionality (e.g., SignalR notifications).
    /// </summary>
    /// <param name="removedMacAddresses">List of MAC addresses that were removed</param>
    protected virtual async Task OnDevicesRemovedAsync(List<string> removedMacAddresses)
    {
        // Base implementation does nothing - override in derived classes
        await Task.CompletedTask;
    }
}
