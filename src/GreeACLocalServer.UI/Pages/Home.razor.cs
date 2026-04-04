using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.ValueObjects;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.UI.Components;
using GreeACLocalServer.UI.Helpers;

namespace GreeACLocalServer.UI.Pages;

public partial class Home(
    IDeviceManagerService _deviceService,
    IDialogService _dialogService,
    NavigationManager _navigation,
    ISnackbar _snackbar) : ComponentBase, IAsyncDisposable
{
    private bool _loading = true;
    private string? _error;
    private List<DeviceDto> _devices = new();
    private HubConnection? _hub;
    private DeviceDetailsDialog? _openDialogComponent;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken _cancellationToken => _cancellationTokenSource.Token;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize SignalR connection
            if (!await InitializeSignalRConnection())
            {
                // Initial fetch as fallback
                var items = await _deviceService.GetAllDeviceStatesAsync(_cancellationToken);
                _devices = items.ToList();
            }
        }
        catch (Exception ex)
        {
            _error = $"Failed to load devices: {ex.Message}";
            Console.WriteLine(_error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task<bool> InitializeSignalRConnection()
    {
        try
        {
            var hubUrl = _navigation.ToAbsoluteUri("/hubs/devices");

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new LinearBackoffRetryPolicy())
                .Build();

            // Set up event handlers
            SetupSignalREventHandlers();

            // Start connection with timeout
            await _hub.StartAsync(_cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection failed: {ex.Message}");
            return false;
        }
    }

    private void SetupSignalREventHandlers()
    {
        if (_hub is null)
        {
            return;
        }

        _hub.On<IEnumerable<DeviceDto>>(DeviceHubMethods.DevicesSnapshot, async snapshot =>
        {
            _devices = snapshot.ToList();
            await InvokeAsync(StateHasChanged);
            await UpdateOpenDialogs();
        });

        _hub.On<DeviceDto>(DeviceHubMethods.DeviceUpserted, async dto =>
        {
            var idx = _devices.FindIndex(d => string.Equals(d.MacAddress, dto.MacAddress, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _devices[idx] = dto;
            }
            else
            {
                _devices.Add(dto);
            }
            await InvokeAsync(StateHasChanged);
            await UpdateOpenDialog(dto);
        });

        _hub.On<string>(DeviceHubMethods.DeviceRemoved, async mac =>
        {
            if (_devices.RemoveAll(d => string.Equals(d.MacAddress, mac, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await InvokeAsync(StateHasChanged);
                CloseDialogForDevice(mac);
            }
        });
    }

    private bool IsDeviceOnline(DeviceDto device)
    {
        // Device is considered offline if last connection was more than 10 minutes ago
        var threshold = DateTime.UtcNow.AddMinutes(-10);
        return device.LastConnectionTimeUtc > threshold;
    }

    private async Task ShowDeviceDetails(DeviceDto device)
    {
        var parameters = new DialogParameters
        {
            { "Device", device },
            { "OnDialogCreated", new Action<DeviceDetailsDialog>(dialog =>
                {
                    _openDialogComponent = dialog;
                })
            },
            { "OnDialogClosed", new Action(() =>
                {
                    _openDialogComponent = null;
                })
            }
        };

        var options = new DialogOptions()
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true
        };

        await _dialogService.ShowAsync<DeviceDetailsDialog>("Device Details", parameters, options);
    }

    private async Task RemoveDevice(DeviceDto device)
    {
        var result = await _dialogService.ShowMessageBoxAsync(
            "Confirm Device Removal",
            $"Are you sure you want to remove device '{device.DNSName}' ({DeviceHelpers.FormatMacAddress(device.MacAddress)})?",
            yesText: "Remove", cancelText: "Cancel");

        if (result == true)
        {
            var success = await _deviceService.RemoveDeviceAsync(device.MacAddress, _cancellationToken);
            if (!success)
            {
                _snackbar.Add($"Failed to remove device '{device.DNSName}'.", Severity.Error);
            }
            else
            {
                _snackbar.Add($"Device removed (${device.MacAddress}).", Severity.Success);
            }
        }
    }

    private async Task UpdateOpenDialogs()
    {
        // Update all open dialogs with latest device data
        foreach (var device in _devices)
        {
            await UpdateOpenDialog(device);
        }
    }

    private async Task UpdateOpenDialog(DeviceDto updatedDevice)
    {
        if (_openDialogComponent?.Device?.MacAddress == updatedDevice.MacAddress)
        {
            await _openDialogComponent.UpdateDevice(updatedDevice);
        }
    }

    private void CloseDialogForDevice(string macAddress)
    {
        if (_openDialogComponent?.Device?.MacAddress == macAddress)
        {
            _openDialogComponent.CloseDialog();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
        }
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
    }
}
