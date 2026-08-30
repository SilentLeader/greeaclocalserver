using GreeACLocalServer.Device.ValueObjects;

namespace GreeACLocalServer.Device.Models;

public class ServerOptions
{
    public string? DomainName { get; set; }
    public string? ExternalIp { get; set; }


    public bool TLSEnabled { get; set; } = false;

    /// <summary>
    /// Plaintext TCP ports the device listener binds. GREE firmware revisions
    /// differ: most use 5000, some connect to 1812, so both are bound by default.
    /// </summary>
    public List<int> TcpPorts { get; set; } = [ServerOption.PORT, ServerOption.ALT_PORT];

    /// <summary>
    /// TCP port for the TLS device listener (only bound when <see cref="TLSEnabled"/>
    /// is true). External implementations use 1813; adjust per firmware.
    /// </summary>
    public int TlsPort { get; set; } = ServerOption.TLS_PORT;

    public List<string> ListenIPAddresses { get; set; } = [];

    /// <summary>
    /// Close an accepted device connection after this many seconds without any
    /// inbound data. GREE devices heartbeat roughly every 10 s, so the default is
    /// comfortably safe. A value &lt;= 0 disables the idle timeout.
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Upper bound on the number of device connections handled concurrently.
    /// Further connections are dropped immediately until a slot frees up.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 200;

    /// <summary>
    /// When true (default) the TLS listener also accepts SSL3 / TLS 1.0 / TLS 1.1
    /// for old AC firmware. Set to false to require TLS 1.2+.
    /// </summary>
    public bool AllowLegacyTlsProtocols { get; set; } = true;

    /// <summary>
    /// Directory for raw dumps of connections that are not the GREE JSON line
    /// protocol (e.g. the binary "fg" frame seen from some newer firmware, or a
    /// stray TLS ClientHello arriving on a plaintext port). Each dump is a
    /// timestamped <c>.bin</c> plus a <c>.txt</c> sidecar with metadata.
    /// Empty / null (default) disables the capture; such connections are still
    /// logged once and closed cleanly either way.
    /// </summary>
    public string? UnknownFrameCapturePath { get; set; }
}