using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GreeACLocalServer.Device.Interfaces;

public interface ISocketHandlerService
{
    void Start();
    void Stop();
}