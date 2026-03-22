using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Device.Interfaces;

internal interface IDeviceEventPublisher
{
    void DeviceConnected(DeviceConnectedMessage message);
}