namespace GreeACLocalServer.Device.Requests;

public class SetDeviceNameRequest(string ipAddress, string deviceName) : DeviceManagementRequestBase(ipAddress)
{
    public string DeviceName { get; } = deviceName;
}