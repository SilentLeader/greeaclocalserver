using System.Security.Cryptography.X509Certificates;

namespace GreeACLocalServer.Device.Interfaces;

internal interface ICryptoService
{
    string Decrypt(string pack, string? key = null);
    string Encrypt(string pack, string? key = null);

    /// <summary>
    /// Get certificate for TLS support
    /// </summary>
    X509Certificate2 GetCertificate(string? hostName = null);
}
