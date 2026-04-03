using GreeACLocalServer.Shared.Contracts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GreeACLocalServer.UI.Components;

public partial class DeviceDetailsDialog : ComponentBase, IDisposable
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter] public DeviceDto Device { get; set; } = default!;

    [Parameter] public Action<DeviceDetailsDialog>? OnDialogCreated { get; set; }

    [Parameter] public Action? OnDialogClosed { get; set; }

    private bool IsOnline => Device.LastConnectionTimeUtc > DateTime.UtcNow.AddMinutes(-10);

    protected override void OnInitialized()
    {
        OnDialogCreated?.Invoke(this);
    }

    public async Task UpdateDevice(DeviceDto updatedDevice)
    {
        Device = updatedDevice;
        StateHasChanged();
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
