namespace GreeACLocalServer.Device.Crypto;

/// <summary>
/// Generation of the local UDP :7000 control protocol a device speaks. The two
/// differ only in how the <c>pack</c> payload is encrypted.
/// </summary>
public enum GreeProtocolGeneration
{
    /// <summary>AES-ECB / PKCS7 (older Wi-Fi modules).</summary>
    V1Ecb,

    /// <summary>AES-GCM with a fixed nonce + AAD and a separate <c>tag</c> field (newer Wi-Fi modules).</summary>
    V2Gcm,
}
