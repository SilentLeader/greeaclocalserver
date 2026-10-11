namespace GreeACLocalServer.Device.Crypto;

/// <summary>
/// Encrypts / decrypts the <c>pack</c> payload of the local UDP :7000 control
/// protocol for one <see cref="GreeProtocolGeneration"/>.
/// </summary>
public interface IGreeCipher
{
    GreeProtocolGeneration Generation { get; }

    /// <param name="plaintext">JSON payload to encrypt.</param>
    /// <param name="key">Device key; the generation's well-known generic key when empty (scan / bind).</param>
    GreeCipherText Encrypt(string plaintext, string? key = null);

    /// <summary>
    /// Decrypts a payload without throwing, so the caller can probe which
    /// generation a device speaks.
    /// </summary>
    /// <param name="pack">Base64 encoded encrypted payload.</param>
    /// <param name="tag">Base64 GCM tag from the envelope; ignored by the ECB generation.</param>
    /// <param name="key">Device key; the generation's well-known generic key when empty.</param>
    /// <param name="plaintext">Decrypted JSON payload when the method returns <c>true</c>.</param>
    bool TryDecrypt(string pack, string? tag, string? key, out string plaintext);
}
