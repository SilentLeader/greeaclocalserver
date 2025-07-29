using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using GreeACHeartBeatServer.Api.Options;

namespace GreeACHeartBeatServer.Api.Services;

public class CryptoService(IOptions<ServerOptions> options)
{
    private readonly string _cryptoKey = options.Value.CryptoKey;

    public string Decrypt(string pack)
    {
        using var myaes = Aes.Create();
        myaes.Mode = CipherMode.ECB;
        myaes.Key = Encoding.UTF8.GetBytes(_cryptoKey);
        myaes.Padding = PaddingMode.PKCS7;
        myaes.GenerateIV();

        using var decryptor = myaes.CreateDecryptor(myaes.Key, myaes.IV);
        using var msDecrypt = new MemoryStream(Convert.FromBase64String(pack));
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        var plaintext = srDecrypt.ReadToEnd();
        return plaintext;
    }

    public string Encrypt(string pack)
    {
        using var myaes = Aes.Create();
        myaes.Mode = CipherMode.ECB;
        myaes.Key = Encoding.UTF8.GetBytes(_cryptoKey);
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