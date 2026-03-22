namespace GreeACLocalServer.Device.Results;

public abstract class ResultBase(
    bool success,
    string message,
    string? errorCode = null)
{
    public bool IsSuccess { get; } = success;
    public string Message { get; } = message;
    public string? ErrorCode { get; } = errorCode;
}