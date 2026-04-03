using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MudBlazor;
using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.ValueObjects;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.UI.Components;
using GreeACLocalServer.UI.Helpers;

namespace GreeACLocalServer.UI.Pages;

public partial class Home : ComponentBase, IAsyncDisposable
{
    [Inject] private IDeviceManagerService DeviceService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private bool _loading = true;
    private string? _error;
    private List<DeviceDto> _devices = new();
    private HubConnection? _hub;
    private DeviceDetailsDialog? _openDialogComponent;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Initialize SignalR connection
            if (!await InitializeSignalRConnection())
            {
                // Initial fetch as fallback
                var items = await DeviceService.GetAllDeviceStatesAsync();
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
            var hubUrl = Navigation.ToAbsoluteUri("/hubs/devices");

            _hub = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new LinearBackoffRetryPolicy())
                .Build();

            // Set up event handlers
            SetupSignalREventHandlers();

            // Start connection with timeout
            await _hub.StartAsync();
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

        await DialogService.ShowAsync<DeviceDetailsDialog>("Device Details", parameters, options);
    }

    private async Task RemoveDevice(DeviceDto device)
    {
        var result = await DialogService.ShowMessageBoxAsync(
            "Confirm Device Removal",
            $"Are you sure you want to remove device '{device.DNSName}' ({DeviceHelpers.FormatMacAddress(device.MacAddress)})?",
            yesText: "Remove", cancelText: "Cancel");

        if (result == true)
        {
            var success = await DeviceService.RemoveDeviceAsync(device.MacAddress);
            if (!success)
            {
                // Show error message if removal failed
                await DialogService.ShowMessageBoxAsync(
                    "Remove Failed",
                    $"Failed to remove device '{device.DNSName}'. The device may have already been removed.",
                    "OK");
            }
            // If successful, the device will be removed from the UI via SignalR notification
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

    private async Task UpdateOpenDialog(GreeACLocalServer.Shared.Contracts.DeviceDto updatedDevice)
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
    }
}
