using GreeACLocalServer.Device.Results;

namespace GreeACLocalServer.Device.Responses;

public class PackCommandResult<TResponse>(
    bool success,
    string message,
    string? errorCode = null,
    TResponse? responseData = default) : ResultBase(success, message, errorCode)
{
    public TResponse? ResponseData { get; } = responseData;
}