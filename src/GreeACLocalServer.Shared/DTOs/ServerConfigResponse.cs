namespace GreeACLocalServer.Shared.DTOs;

public class ServerConfigResponse
{
    public bool EnableManagement { get; set; }
    public bool EnableUI { get; set; }

    /// <summary>
    /// Minutes a device may go without contacting the server before it is
    /// considered offline. Mirrors the server-side DeviceManager:DeviceTimeoutMinutes.
    /// </summary>
    public int DeviceTimeoutMinutes { get; set; } = 60;
}
