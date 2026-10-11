using GreeACLocalServer.Device.Crypto;

namespace GreeACLocalServer.Device.Results;

public class ScanResult(
    bool success,
    string message,
    string? errorCode = null,
    string? macAddress = null,
    string? cryptoKey = null,
    GreeProtocolGeneration generation = GreeProtocolGeneration.V1Ecb) : ResultBase(success, message, errorCode)
{
    public string? MacAddress { get; } = macAddress;

    public string? CryptoKey { get; } = cryptoKey;

    /// <summary>
    /// Protocol generation the bind succeeded with; pack commands must use the same one.
    /// </summary>
    public GreeProtocolGeneration Generation { get; } = generation;
}