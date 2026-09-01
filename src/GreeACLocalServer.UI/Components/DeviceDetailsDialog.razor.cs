using GreeACLocalServer.Shared.Contracts;
using GreeACLocalServer.Shared.Interfaces;
using GreeACLocalServer.UI.Helpers;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GreeACLocalServer.UI.Components;

public partial class DeviceDetailsDialog : ComponentBase, IDisposable
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject] private IDeviceManagerService DeviceManager { get; set; } = default!;

    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private bool _refreshingFirmware;

    private bool _refreshingState;

    [Parameter] public DeviceDto Device { get; set; } = default!;

    [Parameter] public Action<DeviceDetailsDialog>? OnDialogCreated { get; set; }

    [Parameter] public Action? OnDialogClosed { get; set; }

    /// <summary>
    /// The "online" timeout window in minutes, sourced from the server config and
    /// passed in by <see cref="Pages.Home"/> so it is not fetched twice over HTTP.
    /// </summary>
    [Parameter] public int DeviceTimeoutMinutes { get; set; } = new Shared.DTOs.ServerConfigResponse().DeviceTimeoutMinutes;

    private bool IsOnline => DeviceHelpers.IsDeviceOnline(Device, DeviceTimeoutMinutes);

    protected override void OnInitialized()
    {
        OnDialogCreated?.Invoke(this);
    }

    public async Task UpdateDevice(DeviceDto updatedDevice)
    {
        // Invoked from a SignalR hub callback, which runs off the component's
        // Dispatcher; marshal the state change back onto it.
        await InvokeAsync(() =>
        {
            Device = updatedDevice;
            StateHasChanged();
        });
    }

    public void CloseDialog()
    {
        MudDialog?.Close();
    }

    private string GetTimeAgo()
    {
        var timeSpan = DateTime.UtcNow - Device.LastConnectionTimeUtc;

        if (timeSpan.TotalDays >= 1)
        {

            return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays == 1 ? "" : "s")} ago";
        }

        if (timeSpan.TotalHours >= 1)
        {

            return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours == 1 ? "" : "s")} ago";
        }

        if (timeSpan.TotalMinutes >= 1)
        {

            return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes == 1 ? "" : "s")} ago";
        }

        return "Just now";
    }

    private async Task RefreshFirmware()
    {
        if (_refreshingFirmware || Device is null)
        {
            return;
        }

        _refreshingFirmware = true;
        StateHasChanged();
        try
        {
            var updated = await DeviceManager.RefreshFirmwareAsync(Device.MacAddress);
            if (updated is not null)
            {
                Device = updated;
                Snackbar.Add("Firmware information refreshed", Severity.Success);
            }
            else
            {
                Snackbar.Add("Could not read firmware from the device", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Firmware refresh failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _refreshingFirmware = false;
            StateHasChanged();
        }
    }

    private async Task RefreshState()
    {
        if (_refreshingState || Device is null)
        {
            return;
        }

        _refreshingState = true;
        StateHasChanged();
        try
        {
            var updated = await DeviceManager.RefreshRuntimeStateAsync(Device.MacAddress);
            if (updated is not null)
            {
                Device = updated;
                Snackbar.Add("Operating state refreshed", Severity.Success);
            }
            else
            {
                Snackbar.Add("Could not read the operating state from the device", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"State refresh failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _refreshingState = false;
            StateHasChanged();
        }
    }

    private void Cancel()
    {
        OnDialogClosed?.Invoke();
        MudDialog?.Close();
    }

    public void Dispose()
    {
        // OnDialogClosed already invoked by MudDialog.Close()
    }
}
