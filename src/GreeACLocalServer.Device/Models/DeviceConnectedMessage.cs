namespace GreeACLocalServer.Device.Models;

public class DeviceConnectedMessage
{
    public required string MacAddress { get; set; }

    public string? IPAddress { get; set; }

    /// <summary>
    /// Local TCP port the device connected to. 0 when the port is unknown.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// True when the connection was accepted on the TLS listener.
    /// </summary>
    public bool IsTls { get; set; }
}
