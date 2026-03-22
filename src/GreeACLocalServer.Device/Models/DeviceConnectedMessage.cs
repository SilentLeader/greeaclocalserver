namespace GreeACLocalServer.Device.Models;

public class DeviceConnectedMessage
{
    public required string MacAddress { get; set; }

    public string? IPAddress { get; set; }
}