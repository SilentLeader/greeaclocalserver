using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.DTOs;
using GreeACLocalServer.Shared.Interfaces;

namespace GreeACLocalServer.UI.Services;

public class HttpConfigService : IConfigService
{
    private readonly HttpClient _httpClient;

    public HttpConfigService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ServerConfigResponse> GetServerConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/config/server", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ServerConfigResponse>(cancellationToken: cancellationToken);
                return result ?? new ServerConfigResponse
                {
                    EnableManagement = false,
                    EnableUI = true
                };
            }
            else
            {
                // Return default values if the API call fails
                return new ServerConfigResponse
                {
                    EnableManagement = false,
                    EnableUI = true
                };
            }
        }
        catch (Exception)
        {
            // Return default values on any error
            return new ServerConfigResponse
            {
                EnableManagement = false,
                EnableUI = true
            };
        }
    }
}
