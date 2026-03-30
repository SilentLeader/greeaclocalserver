namespace GreeACLocalServer.Device.Models;

public class EncryptionOptions
{
    public string? DefaultCryptoKey { get; set; }

    /// <summary>
    /// Create self signed certificate if it is not exists
    /// </summary>
    public bool TLSCertificateAutoCreate { get; set; } = true;

    /// <summary>
    /// TLS certificate path for TLS support
    /// </summary>
    public string? TLSCertificatePath { get; set; }

    public string? TLSCertificatePassword { get; set; }
}