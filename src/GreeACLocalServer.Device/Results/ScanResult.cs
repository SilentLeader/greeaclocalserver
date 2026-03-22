namespace GreeACLocalServer.Device.Results;

public class ScanResult(
    bool success,
    string message,
    string? errorCode = null,
    string? macAddress = null,
    string? cryptoKey = null) : ResultBase(success, message, errorCode)
{
    public string? MacAddress { get; } = macAddress;

    public string? CryptoKey { get; } = cryptoKey;
}