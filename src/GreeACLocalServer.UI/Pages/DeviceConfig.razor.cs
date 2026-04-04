using Microsoft.AspNetCore.Components;
using MudBlazor;
using GreeACLocalServer.Shared.DTOs;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.Shared.Contracts;

namespace GreeACLocalServer.UI.Pages;

public partial class DeviceConfig(
    IDeviceConfigService _deviceConfigService,
    IDeviceManagerService _deviceManagerService,
    IConfigService _configService,
    ISnackbar _snackbar) : ComponentBase, IDisposable
{
    private readonly QueryDeviceStatusRequest _statusRequest = new();
    private readonly UpdateDeviceNameRequest _setNameRequest = new();
    private readonly UpdateRemoteHostRequest _setHostRequest = new();

    private DeviceStatusResponse? _statusResponse;
    private DeviceOperationResponse? _setNameResponse;
    private DeviceOperationResponse? _setHostResponse;
    private ServerConfigResponse? _serverConfig;

    private bool _queryInProgress = false;
    private bool _setNameInProgress = false;
    private bool _setHostInProgress = false;
    private bool _loadingDevices = false;
    private bool _loadingConfig = false;

    private CancellationTokenSource _cancellationTokenSource = new();
    private List<DeviceDto> _availableDevices = new();

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            LoadAvailableDevicesAsync(),
            LoadServerConfigAsync()
        );
    }

    private async Task LoadServerConfigAsync()
    {
        try
        {
            _loadingConfig = true;
            StateHasChanged();

            _serverConfig = await _configService.GetServerConfigAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Failed to load server configuration: {ex.Message}", Severity.Warning);
            // Set default config if loading fails
            _serverConfig = new ServerConfigResponse
            {
                EnableManagement = false,
                EnableUI = true
            };
        }
        finally
        {
            _loadingConfig = false;
            StateHasChanged();
        }
    }

    private async Task LoadAvailableDevicesAsync()
    {
        try
        {
            _loadingDevices = true;
            StateHasChanged();

            _availableDevices = (await _deviceManagerService.GetAllDeviceStatesAsync(_cancellationTokenSource.Token)).ToList();
        }
        catch (Exception ex)
        {
            _snackbar.Add($"Failed to load available devices: {ex.Message}", Severity.Warning);
        }
        finally
        {
            _loadingDevices = false;
            StateHasChanged();
        }
    }

    private async Task<IEnumerable<string>> SearchDeviceIpAddresses(string value, CancellationToken cancellationToken)
    {
        // If no search value, return all IP addresses
        if (string.IsNullOrWhiteSpace(value))
        {
            return _availableDevices.Select(d => d.IpAddress).Distinct().OrderBy(ip => ip);
        }

        // Filter IP addresses based on search value
        var searchValue = value.Trim().ToLowerInvariant();
        return _availableDevices
            .Where(d => d.IpAddress.ToLowerInvariant().Contains(searchValue) ||
                       (!string.IsNullOrEmpty(d.DNSName) && d.DNSName.ToLowerInvariant().Contains(searchValue)))
            .Select(d => d.IpAddress)
            .Distinct()
            .OrderBy(ip => ip);
    }

    private async Task QueryDeviceStatus()
    {
        if (_queryInProgress)
        {
            return;
        }

        _queryInProgress = true;
        _statusResponse = null;
        StateHasChanged();

        try
        {
            _statusResponse = await _deviceConfigService.QueryDeviceStatusAsync(_statusRequest, _cancellationTokenSource.Token);

            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (_statusResponse.Success)
            {
                _snackbar.Add("Device status retrieved successfully!", Severity.Success);
            }
            else
            {
                _snackbar.Add($"Failed to query device status: {_statusResponse.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _statusResponse = new DeviceStatusResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION"
            };
            _snackbar.Add($"Error querying device: {ex.Message}", Severity.Error);
        }
        finally
        {
            _queryInProgress = false;
            StateHasChanged();
        }
    }

    private async Task SetDeviceName()
    {
        if (_setNameInProgress) return;

        _setNameInProgress = true;
        _setNameResponse = null;
        StateHasChanged();

        try
        {
            _setNameResponse = await _deviceConfigService.SetDeviceNameAsync(_setNameRequest, _cancellationTokenSource.Token);

            if (_setNameResponse.Success)
            {
                _snackbar.Add("Device name set successfully!", Severity.Success);
                // Clear the form
                _setNameRequest.IpAddress = string.Empty;
                _setNameRequest.DeviceName = string.Empty;
            }
            else
            {
                _snackbar.Add($"Failed to set device name: {_setNameResponse.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _setNameResponse = new DeviceOperationResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION"
            };
            _snackbar.Add($"Error setting device name: {ex.Message}", Severity.Error);
        }
        finally
        {
            _setNameInProgress = false;
            StateHasChanged();
        }
    }

    private async Task SetRemoteHost()
    {
        if (_setHostInProgress)
        {
            return;
        }

        _setHostInProgress = true;
        _setHostResponse = null;
        StateHasChanged();

        try
        {
            _setHostResponse = await _deviceConfigService.SetRemoteHostAsync(_setHostRequest, _cancellationTokenSource.Token);

            if (_setHostResponse.Success)
            {
                _snackbar.Add("Remote host set successfully! Remember to power cycle the device.", Severity.Success);
                // Clear the form
                _setHostRequest.IpAddress = string.Empty;
                _setHostRequest.RemoteHost = string.Empty;
            }
            else
            {
                _snackbar.Add($"Failed to set remote host: {_setHostResponse.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            _setHostResponse = new DeviceOperationResponse
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = "EXCEPTION"
            };
            _snackbar.Add($"Error setting remote host: {ex.Message}", Severity.Error);
        }
        finally
        {
            _setHostInProgress = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
