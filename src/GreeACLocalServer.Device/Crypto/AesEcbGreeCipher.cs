using System.Security.Cryptography;
using GreeACLocalServer.Device.Interfaces;

namespace GreeACLocalServer.Device.Crypto;

/// <summary>
/// V1 generation: AES-ECB / PKCS7 via <see cref="ICryptoService"/>, the same
/// cipher (and configured default key) the inbound device connection uses.
/// </summary>
public sealed class AesEcbGreeCipher(ICryptoService cryptoService) : IGreeCipher
{
    public GreeProtocolGeneration Generation => GreeProtocolGeneration.V1Ecb;

    public GreeCipherText Encrypt(string plaintext, string? key = null)
        => new(cryptoService.Encrypt(plaintext, key), null);

    public bool TryDecrypt(string pack, string? tag, string? key, out string plaintext)
    {
        try
        {
            plaintext = cryptoService.Decrypt(pack, key);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            plaintext = string.Empty;
            return false;
        }
    }
}
