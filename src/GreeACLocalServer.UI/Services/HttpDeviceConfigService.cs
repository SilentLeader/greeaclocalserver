using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.DTOs;
using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.UI.Services;

public class HttpDeviceConfigService : IDeviceConfigService
{
    private readonly HttpClient _httpClient;

    public HttpDeviceConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DeviceStatusResponse> QueryDeviceStatusAsync(DeviceStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/device-config/status", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DeviceStatusResponse>(cancellationToken: cancellationToken);
                return result ?? new DeviceStatusResponse
                {
                    Success = false,
                    Message = "Failed to parse response",
                    ErrorCode = "PARSE_ERROR"
                };
            }

            return new DeviceStatusResponse
            {
                Success = false,
                Message = $"API request failed: {response.StatusCode}",
                ErrorCode = "API_ERROR"
            };
        }
        catch (Exception ex)
        {
            return new DeviceStatusResponse
            {
                Success = false,
                Message = $"Request failed: {ex.Message}",
                ErrorCode = "REQUEST_EXCEPTION"
            };
        }
    }

    public async Task<DeviceOperationResponse> SetDeviceNameAsync(SetDeviceNameRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/device-config/set-name", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DeviceOperationResponse>(cancellationToken: cancellationToken);
                return result ?? new DeviceOperationResponse
                {
                    Success = false,
                    Message = "Failed to parse response",
                    ErrorCode = "PARSE_ERROR"
                };
            }

            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"API request failed: {response.StatusCode}",
                ErrorCode = "API_ERROR"
            };
        }
        catch (Exception ex)
        {
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Request failed: {ex.Message}",
                ErrorCode = "REQUEST_EXCEPTION"
            };
        }
    }

    public async Task<DeviceOperationResponse> SetRemoteHostAsync(SetRemoteHostRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/device-config/set-remote-host", request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DeviceOperationResponse>(cancellationToken: cancellationToken);
                return result ?? new DeviceOperationResponse
                {
                    Success = false,
                    Message = "Failed to parse response",
                    ErrorCode = "PARSE_ERROR"
                };
            }

            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"API request failed: {response.StatusCode}",
                ErrorCode = "API_ERROR"
            };
        }
        catch (Exception ex)
        {
            return new DeviceOperationResponse
            {
                Success = false,
                Message = $"Request failed: {ex.Message}",
                ErrorCode = "REQUEST_EXCEPTION"
            };
        }
    }
}
