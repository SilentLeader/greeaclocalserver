using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Device.Interfaces;

public interface IDeviceEventHandlerService
{
    event EventHandler<DeviceConnectedMessage>? OnDeviceConnected;
}