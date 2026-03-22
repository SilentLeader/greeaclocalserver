
namespace GreeACLocalServer.Device.Results;

public class SimpleDeviceOperationResult(bool success, string message, string? errorCode = null) : ResultBase(success, message, errorCode)
{

}