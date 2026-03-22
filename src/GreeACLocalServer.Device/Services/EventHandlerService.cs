using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Device.Services;

internal class EventHandlerService : IDeviceEventPublisher, IDeviceEventHandlerService
{
    public event EventHandler<DeviceConnectedMessage>? OnDeviceConnected;

    public void DeviceConnected(DeviceConnectedMessage message)
    {
        OnDeviceConnected?.Invoke(this, message);
    }
}