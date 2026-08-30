using System.Collections.Concurrent;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Base device manager service that provides core functionality without SignalR dependencies.
/// Can be used directly for headless mode or inherited by DeviceManagerService for UI mode.
/// </summary>
public class HeadlessDeviceManagerService(
    IDnsResolverService dnsResolver) : IInternalDeviceManagerService

{
    protected readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();
    protected readonly IDnsResolverService _dnsResolver = dnsResolver;

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
                Endpoints = MergeEndpoint(existing.Endpoints, port, isTls, now)
            });

        // Virtual method hook for derived classes (e.g., SignalR notifications)
        await OnDeviceUpdatedAsync(state);
    }

    public virtual Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<DeviceDto> result = _deviceStates.Values.Select(ToDto);
        return Task.FromResult(result);
    }

    public virtual Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (_deviceStates.TryGetValue(macAddress, out var state))
        {
            return Task.FromResult<DeviceDto?>(ToDto(state));
        }
        return Task.FromResult<DeviceDto?>(null);
    }

    /// <summary>Projects the internal device state onto the wire contract.</summary>
    protected static DeviceDto ToDto(AcDeviceState state) => new(
        state.MacAddress,
        state.IpAddress,
        state.DNSName,
        state.LastConnectionTime,
        state.Endpoints.Select(e => new DeviceEndpointDto(e.Port, e.IsTls, e.LastSeenUtc)).ToList());

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
