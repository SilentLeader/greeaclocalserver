
namespace GreeACLocalServer.Device.Models;

public class ServerOptions
{
    public string? DomainName { get; set; }
    public string? ExternalIp { get; set; }

    public List<string> ListenIPAddresses { get; set; } = new();
}