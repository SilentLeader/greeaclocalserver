namespace GreeACLocalServer.Device.Requests;

public abstract class DeviceManagementRequestBase(string ipAddress)
{
    public string IpAddress { get; } = ipAddress;
}