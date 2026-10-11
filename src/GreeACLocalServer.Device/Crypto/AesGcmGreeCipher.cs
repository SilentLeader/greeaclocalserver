using System.Security.Cryptography;
using System.Text;

namespace GreeACLocalServer.Device.Crypto;

/// <summary>
/// V2 generation: AES-GCM with the protocol's fixed nonce and associated data.
/// The 16 byte authentication tag travels base64 encoded in a separate
/// top-level <c>tag</c> field next to <c>pack</c>.
/// </summary>
/// <remarks>
/// Reusing one nonce for every message under the same key is cryptographically
/// wrong for GCM, but it is what the devices implement - the constants below are
/// fixed by the protocol and must not be "improved".
/// </remarks>
public sealed class AesGcmGreeCipher : IGreeCipher
{
    /// <summary>Well-known V2 key used for scan / bind, before the device hands out its own key.</summary>
    public const string GenericKey = "{yxAHAY_Lm6pbC/<";

    private const int TagSizeInBytes = 16;

    private static readonly byte[] Nonce = [0x54, 0x40, 0x78, 0x44, 0x49, 0x67, 0x5A, 0x51, 0x6C, 0x5E, 0x63, 0x13];

    private static readonly byte[] AssociatedData = "qualcomm-test"u8.ToArray();

    /// <summary>False on a platform without an AES-GCM implementation; the V2 generation is then unavailable.</summary>
    public static bool IsSupported => AesGcm.IsSupported;

    public GreeProtocolGeneration Generation => GreeProtocolGeneration.V2Gcm;

    public GreeCipherText Encrypt(string plaintext, string? key = null)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plainBytes.Length];
        var tagBytes = new byte[TagSizeInBytes];

        using var aes = CreateAes(key);
        aes.Encrypt(Nonce, plainBytes, cipherBytes, tagBytes, AssociatedData);

        return new GreeCipherText(Convert.ToBase64String(cipherBytes), Convert.ToBase64String(tagBytes));
    }

    public bool TryDecrypt(string pack, string? tag, string? key, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        try
        {
            var cipherBytes = Convert.FromBase64String(pack);
            var tagBytes = Convert.FromBase64String(tag);
            var plainBytes = new byte[cipherBytes.Length];

            using var aes = CreateAes(key);
            aes.Decrypt(Nonce, cipherBytes, tagBytes, plainBytes, AssociatedData);

            // Some firmware pads the JSON with trailing bytes; keep only the object.
            var text = Encoding.UTF8.GetString(plainBytes);
            var end = text.LastIndexOf('}');
            plaintext = end >= 0 ? text[..(end + 1)] : text;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            // Wrong key / generation (tag mismatch), malformed base64 or an unusable key length.
            return false;
        }
    }

    private static AesGcm CreateAes(string? key)
        => new(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(key) ? GenericKey : key), TagSizeInBytes);
}
