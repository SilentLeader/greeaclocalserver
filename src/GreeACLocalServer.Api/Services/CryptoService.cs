using System.Security.Cryptography;
using System.Text;

namespace GreeACLocalServer.Api.Services;

public class CryptoService(IOptions<ServerOptions> options) : ICryptoService
{
    private readonly string _cryptoKey = string.IsNullOrEmpty(options.Value.CryptoKey) ? throw new InvalidOperationException("ServerOptions:CryptoKey must be configured.") : options.Value.CryptoKey;

    /// <summary>
    /// Decrypt with a custom key (used for device communication)
    /// </summary>
    /// <param name="pack">Base64 encoded encrypted data</param>
    /// <param name="key">Encryption key to use (uses default device key if empty)</param>
    /// <returns>Decrypted plaintext</returns>
    public string Decrypt(string pack, string? key = null)
    {
        if (string.IsNullOrEmpty(key))
            key = _cryptoKey;

        using var myaes = Aes.Create();
        myaes.Mode = CipherMode.ECB;
        myaes.Key = Encoding.UTF8.GetBytes(key);
        myaes.Padding = PaddingMode.PKCS7;
        myaes.GenerateIV();

        using var decryptor = myaes.CreateDecryptor(myaes.Key, myaes.IV);
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(pack));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        var plaintext = srDecrypt.ReadToEnd();
        return plaintext;
    }

    /// <summary>
    /// Encrypt with a custom key (used for device communication)
    /// </summary>
    /// <param name="pack">Plaintext data to encrypt</param>
    /// <param name="key">Encryption key to use (uses default device key if empty)</param>
    /// <returns>Base64 encoded encrypted data</returns>
    public string Encrypt(string pack, string? key = null)
    {
        if (string.IsNullOrEmpty(key))
            key = _cryptoKey;

        using var myaes = Aes.Create();
        myaes.Mode = CipherMode.ECB;
        myaes.Key = Encoding.UTF8.GetBytes(key);
        myaes.Padding = PaddingMode.PKCS7;
        myaes.GenerateIV();

        using var encryptor = myaes.CreateEncryptor(myaes.Key, myaes.IV);
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        using var swEncrypt = new StreamWriter(csEncrypt);
        swEncrypt.Write(pack);
        swEncrypt.Flush();
        swEncrypt.Close();
        msEncrypt.Flush();
        var encrypted = Convert.ToBase64String(msEncrypt.ToArray());

        return encrypted;
    }
}