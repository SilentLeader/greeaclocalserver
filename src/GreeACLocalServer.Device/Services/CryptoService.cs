using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreeACLocalServer.Device.Services;

internal class CryptoService(IOptionsMonitor<EncryptionOptions> options, ILogger<CryptoService> _logger) : ICryptoService
{
    private string _defaultCryptoKey => string.IsNullOrEmpty(options.CurrentValue.DefaultCryptoKey)
        ? throw new InvalidOperationException("GreeServer:EncryptionOptions:DefaultCryptoKey must be configured.")
        : options.CurrentValue.DefaultCryptoKey;

    private EncryptionOptions _options => options.CurrentValue;

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

        try
        {
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
        catch
        {
            _logger.LogError("Decryption error. Source data: {pack}", pack);
            throw;
        }
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

    public X509Certificate2 GetCertificate(string? hostName = null)
    {
        if (!string.IsNullOrWhiteSpace(_options.TLSCertificatePath)
            && File.Exists(_options.TLSCertificatePath))
        {
            var ext = Path.GetExtension(_options.TLSCertificatePath);
            var isPkcs12 = ext.Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".p12", StringComparison.OrdinalIgnoreCase);

            if (isPkcs12)
            {
                return X509CertificateLoader.LoadPkcs12FromFile(
                    _options.TLSCertificatePath,
                    string.IsNullOrEmpty(_options.TLSCertificatePassword) ? null : _options.TLSCertificatePassword);
            }

            return X509CertificateLoader.LoadCertificateFromFile(_options.TLSCertificatePath);
        }

        if (_options.TLSCertificateAutoCreate)
        {
            var certificate = GenerateSelfSignedCert(hostName ?? "localhost");
            if (!string.IsNullOrWhiteSpace(_options.TLSCertificatePath))
            {
                var export = certificate.Export(
                    X509ContentType.Pfx,
                    string.IsNullOrEmpty(_options.TLSCertificatePassword) ? null : _options.TLSCertificatePassword);

                File.WriteAllBytes(_options.TLSCertificatePath, export);
            }

            return certificate;
        }

        throw new InvalidOperationException(
            $"TLS certificate not found at '{_options.TLSCertificatePath}' and GreeServer:EncryptionOptions:TLSCertificateAutoCreate is disabled.");
    }

    private X509Certificate2 GenerateSelfSignedCert(string commonName, string organization = "Gree", string unit = "Unit")
    {
        using var rsa = RSA.Create(2048);

        var request = new CertificateRequest(
            $"CN={commonName}, O={organization}, OU={unit}, L=Locality, ST=State, C=XX",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // serverAuth
                false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(commonName);
        if (!string.Equals(commonName, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            sanBuilder.AddDnsName("localhost");
        }

        if (IPAddress.TryParse(commonName, out var ip))
        {
            sanBuilder.AddIpAddress(ip);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());

        var cert = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddYears(10));

        _logger.LogDebug("Generated self-signed certificate with common name: {commonName}", commonName);

        return cert;
    }
}