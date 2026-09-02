using System.Net.Security;
using System.Security.Authentication;
using GreeACLocalServer.Device.Services;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Guards WP-08 (finding F15.1): the legacy TLS protocol set is a config switch.
/// </summary>
public class TlsProtocolResolutionTests
{
#pragma warning disable CS0618, SYSLIB0039 // legacy protocols referenced for assertions
    [Fact]
    public void ResolveProtocols_AllowLegacy_IncludesSsl3ThroughTls13()
    {
        var protocols = SocketHandlerService.ResolveProtocols(allowLegacy: true);

        Assert.True(protocols.HasFlag(SslProtocols.Ssl3));
        Assert.True(protocols.HasFlag(SslProtocols.Tls));
        Assert.True(protocols.HasFlag(SslProtocols.Tls11));
        Assert.True(protocols.HasFlag(SslProtocols.Tls12));
        Assert.True(protocols.HasFlag(SslProtocols.Tls13));
    }

    [Fact]
    public void ResolveProtocols_DisallowLegacy_IsTls12AndTls13Only()
    {
        var protocols = SocketHandlerService.ResolveProtocols(allowLegacy: false);

        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, protocols);
        Assert.False(protocols.HasFlag(SslProtocols.Ssl3));
        Assert.False(protocols.HasFlag(SslProtocols.Tls));
        Assert.False(protocols.HasFlag(SslProtocols.Tls11));
    }
#pragma warning restore CS0618, SYSLIB0039

    [Fact]
    public void LegacyDeviceCipherSuiteList_IncludesLegacyRsaCbcShaSuites()
    {
        var suites = SocketHandlerService.LegacyDeviceCipherSuiteList;

        Assert.Contains(TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA, suites);
        Assert.Contains(TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA, suites);
    }

    [Fact]
    public void LegacyDeviceCipherSuiteList_IncludesAtLeastOneTls13AeadSuite()
    {
        var suites = SocketHandlerService.LegacyDeviceCipherSuiteList;

        Assert.Contains(suites, s =>
            s is TlsCipherSuite.TLS_AES_256_GCM_SHA384
              or TlsCipherSuite.TLS_AES_128_GCM_SHA256
              or TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256);
    }

    [Fact]
    public void LegacyDeviceCipherSuiteList_HasNoNullRc4OrExportSuites()
    {
        foreach (var suite in SocketHandlerService.LegacyDeviceCipherSuiteList)
        {
            var name = suite.ToString();
            Assert.DoesNotContain("NULL", name, StringComparison.Ordinal);
            Assert.DoesNotContain("RC4", name, StringComparison.Ordinal);
            Assert.DoesNotContain("EXPORT", name, StringComparison.Ordinal);
            Assert.DoesNotContain("anon", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
