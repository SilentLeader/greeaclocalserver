namespace GreeACLocalServer.Device.Results;

public class DeviceStatusResult(bool success,
    string message,
    string? errorCode = null,
    string? deviceName = null,
    string? remoteHost = null,
    string? macAddress = null) : ResultBase(success, message, errorCode)
{
    public string? DeviceName { get; } = deviceName;

    public string? RemoteHost { get; } = remoteHost;

    public string? MacAddress { get; } = macAddress;
}