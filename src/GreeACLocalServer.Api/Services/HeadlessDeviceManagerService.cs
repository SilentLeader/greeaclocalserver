using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.Interfaces;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using GreeACLocalServer.Api.Models;

namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Base device manager service that provides core functionality without SignalR dependencies.
/// Can be used directly for headless mode or inherited by DeviceManagerService for UI mode.
/// </summary>
public class HeadlessDeviceManagerService(IOptions<DeviceManagerOptions> options, IDnsResolverService dnsResolver) : IInternalDeviceManagerService
{
    protected readonly ConcurrentDictionary<string, AcDeviceState> _deviceStates = new();
    protected readonly DeviceManagerOptions _options = options.Value;
    protected readonly IDnsResolverService _dnsResolver = dnsResolver;

    public virtual async Task UpdateOrAddAsync(string macAddress, string ipAddress)
    {
        var dnsName = await _dnsResolver.ResolveDnsNameAsync(ipAddress);
        
        var state = _deviceStates.AddOrUpdate(macAddress,
            key => new AcDeviceState
            {
                MacAddress = macAddress,
                IpAddress = ipAddress,
                DNSName = dnsName,
                LastConnectionTime = DateTime.UtcNow
            },
            (key, existing) =>
            {
                existing.IpAddress = ipAddress;
                existing.DNSName = dnsName;
                existing.LastConnectionTime = DateTime.UtcNow;
                return existing;
            });

        // Virtual method hook for derived classes (e.g., SignalR notifications)
        await OnDeviceUpdatedAsync(state);
    }

    public virtual async Task RemoveStaleDevicesAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_options.DeviceTimeoutMinutes);
        var removed = new List<string>();
        foreach (var kvp in _deviceStates)
        {
            if (kvp.Value.LastConnectionTime < threshold)
            {
                if (_deviceStates.TryRemove(kvp.Key, out _))
                {
                    removed.Add(kvp.Key);
                }
            }
        }

        // Virtual method hook for derived classes (e.g., SignalR notifications)
        await OnDevicesRemovedAsync(removed);
    }

    public virtual Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default)
    {
        // Remove automatic stale device removal
        IEnumerable<DeviceDto> result = _deviceStates.Values.Select(v => new DeviceDto(v.MacAddress, v.IpAddress, v.DNSName, v.LastConnectionTime));
        return Task.FromResult(result);
    }

    public virtual Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        // Remove automatic stale device removal
        if (_deviceStates.TryGetValue(macAddress, out var state))
        {
            DeviceDto dto = new DeviceDto(state.MacAddress, state.IpAddress, state.DNSName, state.LastConnectionTime);
            return Task.FromResult<DeviceDto?>(dto);
        }
        return Task.FromResult<DeviceDto?>(null);
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
