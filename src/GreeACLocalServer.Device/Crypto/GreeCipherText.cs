namespace GreeACLocalServer.Device.Crypto;

/// <summary>
/// An encrypted <c>pack</c> payload. <paramref name="Tag"/> is the base64 GCM
/// authentication tag and is <c>null</c> for the ECB generation.
/// </summary>
public readonly record struct GreeCipherText(string Pack, string? Tag);
