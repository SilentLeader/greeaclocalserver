using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

public class CryptoServiceTests
{
    private const string DefaultKey = "a3K8Bx%2r8Y7#xDh"; // 16 chars

    private static CryptoService CreateService(EncryptionOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<EncryptionOptions>>();
        monitor.Setup(x => x.CurrentValue).Returns(options);
        return new CryptoService(monitor.Object, NullLogger<CryptoService>.Instance);
    }

    [Fact]
    public void Encrypt_Decrypt_Roundtrip_DefaultKey()
    {
        var service = CreateService(new EncryptionOptions { DefaultCryptoKey = DefaultKey });

        var plaintext = "{\"t\":\"pack\",\"value\":42}";
        var roundtrip = service.Decrypt(service.Encrypt(plaintext));

        Assert.Equal(plaintext, roundtrip);
    }

    [Fact]
    public void Encrypt_Decrypt_Roundtrip_ExplicitKey()
    {
        var service = CreateService(new EncryptionOptions { DefaultCryptoKey = DefaultKey });

        var explicitKey = "1234567890abcdef";
        var plaintext = "hello device";
        var roundtrip = service.Decrypt(service.Encrypt(plaintext, explicitKey), explicitKey);

        Assert.Equal(plaintext, roundtrip);
    }

    [Fact]
    public void GetCertificate_AutoCreate_NoPath_ReturnsCertWithPrivateKeyAndCn()
    {
        var service = CreateService(new EncryptionOptions
        {
            DefaultCryptoKey = DefaultKey,
            TLSCertificateAutoCreate = true,
            TLSCertificatePath = null,
        });

        using var cert = service.GetCertificate("gree.example.com");

        Assert.True(cert.HasPrivateKey);
        Assert.Contains("gree.example.com", cert.Subject);
    }

    [Fact]
    public void GetCertificate_AutoCreate_PersistsPkcs12_NoPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gree-test-{Guid.NewGuid():N}.pfx");
        try
        {
            var service = CreateService(new EncryptionOptions
            {
                DefaultCryptoKey = DefaultKey,
                TLSCertificateAutoCreate = true,
                TLSCertificatePath = path,
            });

            using (service.GetCertificate("host.local")) { }

            Assert.True(File.Exists(path));
            using var reloaded = X509CertificateLoader.LoadPkcs12FromFile(path, null);
            Assert.True(reloaded.HasPrivateKey);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GetCertificate_AutoCreate_PersistsPkcs12_WithPassword()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gree-test-{Guid.NewGuid():N}.pfx");
        const string password = "s3cr3t";
        try
        {
            var service = CreateService(new EncryptionOptions
            {
                DefaultCryptoKey = DefaultKey,
                TLSCertificateAutoCreate = true,
                TLSCertificatePath = path,
                TLSCertificatePassword = password,
            });

            using (service.GetCertificate("host.local")) { }

            Assert.True(File.Exists(path));
            using var reloaded = X509CertificateLoader.LoadPkcs12FromFile(path, password);
            Assert.True(reloaded.HasPrivateKey);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GetCertificate_AutoCreate_LoadsBackPersistedPfx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gree-test-{Guid.NewGuid():N}.pfx");
        try
        {
            var options = new EncryptionOptions
            {
                DefaultCryptoKey = DefaultKey,
                TLSCertificateAutoCreate = true,
                TLSCertificatePath = path,
            };
            var service = CreateService(options);

            using var created = service.GetCertificate("host.local");
            using var loaded = service.GetCertificate("host.local"); // now file exists -> load path

            Assert.True(loaded.HasPrivateKey);
            Assert.Equal(created.Thumbprint, loaded.Thumbprint);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GetCertificate_GeneratedCert_HasSanForHostName()
    {
        var service = CreateService(new EncryptionOptions
        {
            DefaultCryptoKey = DefaultKey,
            TLSCertificateAutoCreate = true,
        });

        using var cert = service.GetCertificate("gree.example.com");

        var san = cert.Extensions["2.5.29.17"];
        Assert.NotNull(san);
        Assert.Contains("gree.example.com", new AsnEncodedData(san.Oid, san.RawData).Format(false));
    }

    [Fact]
    public void GetCertificate_GeneratedCert_HasServerAuthEkuAndIsCurrentlyValid()
    {
        var service = CreateService(new EncryptionOptions
        {
            DefaultCryptoKey = DefaultKey,
            TLSCertificateAutoCreate = true,
        });

        using var cert = service.GetCertificate("gree.example.com");

        var eku = cert.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        Assert.Contains(eku.EnhancedKeyUsages.Cast<Oid>(), o => o.Value == "1.3.6.1.5.5.7.3.1");

        Assert.True(cert.NotBefore <= DateTime.Now);
        Assert.True(cert.NotAfter > DateTime.Now);
    }

    [Fact]
    public void GetCertificate_NoAutoCreate_NoFile_Throws()
    {
        var service = CreateService(new EncryptionOptions
        {
            DefaultCryptoKey = DefaultKey,
            TLSCertificateAutoCreate = false,
            TLSCertificatePath = null,
        });

        Assert.Throws<InvalidOperationException>(() => service.GetCertificate("host.local"));
    }
}
