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
}
