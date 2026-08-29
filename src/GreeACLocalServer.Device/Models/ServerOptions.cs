
namespace GreeACLocalServer.Device.Models;

public class ServerOptions
{
    public string? DomainName { get; set; }
    public string? ExternalIp { get; set; }


    public bool TLSEnabled { get; set; } = false;

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
}