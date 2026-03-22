using System.Security.Cryptography;
using System.Text;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using Microsoft.Extensions.Options;

namespace GreeACLocalServer.Device.Services;

internal class CryptoService(IOptionsMonitor<DeviceManagementOptions> _options) : ICryptoService
{
    private string _defaultCryptoKey => string.IsNullOrEmpty(_options.CurrentValue.DefaultCryptoKey)
        ? throw new InvalidOperationException("GreeServer:DeviceManagementOptions:DefaultCryptoKey must be configured.")
        : _options.CurrentValue.DefaultCryptoKey;

    /// <summary>
    /// Decrypt with a custom key (used for device communication)
    /// </summary>
    /// <param name="pack">Base64 encoded encrypted data</param>
    /// <param name="key">Encryption key to use (uses default device key if empty)</param>
    /// <returns>Decrypted plaintext</returns>
    public string Decrypt(string pack, string? key = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            key = _defaultCryptoKey;
        }

        using var myaes = Aes.Create();
        myaes.Mode = CipherMode.ECB;
        myaes.Key = Encoding.UTF8.GetBytes(key);
        myaes.Padding = PaddingMode.PKCS7;
        myaes.GenerateIV();

        using var decryptor = myaes.CreateDecryptor(myaes.Key, myaes.IV);
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(pack));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        return srDecrypt.ReadToEnd();
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
        {
            key = _defaultCryptoKey;
        }


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
        return Convert.ToBase64String(msEncrypt.ToArray());
    }
}