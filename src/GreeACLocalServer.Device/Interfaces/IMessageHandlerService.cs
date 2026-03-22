using GreeACLocalServer.Device.Responses;

namespace GreeACLocalServer.Device.Interfaces;

public interface IMessageHandlerService
{
    GreeHandlerResponse GetResponse(string input);
}