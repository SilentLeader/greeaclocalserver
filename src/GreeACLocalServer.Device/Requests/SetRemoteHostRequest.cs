namespace GreeACLocalServer.Device.Requests;

public class SetRemoteHostRequest(string ipAddress, string remoteHost) : DeviceManagementRequestBase(ipAddress)
{
    public string RemoteHost { get; } = remoteHost;

}