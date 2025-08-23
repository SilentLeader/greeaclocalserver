using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.UI.Services;

public class HttpDeviceManagerService(HttpClient httpClient) : IDeviceManagerService
{
    private readonly HttpClient _http = httpClient;

    public async Task<IEnumerable<DeviceDto>> GetAllDeviceStatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await _http.GetFromJsonAsync<List<DeviceDto>>("api/devices", cancellationToken).ConfigureAwait(false);
            return devices ?? Enumerable.Empty<DeviceDto>();
        }
        catch
        {
            return Enumerable.Empty<DeviceDto>();
        }
    }

    public async Task<DeviceDto?> GetAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) 
        {
            return null;
        }
        try
        {
            var response = await _http.GetAsync($"api/devices/{Uri.EscapeDataString(macAddress)}", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DeviceDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RemoveDeviceAsync(string macAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(macAddress)) 
        {
            return false;
        }
        try
        {
            var response = await _http.DeleteAsync($"api/devices/{Uri.EscapeDataString(macAddress)}", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
