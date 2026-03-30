
namespace GreeACLocalServer.Device.Models;

public class ServerOptions
{
    public string? DomainName { get; set; }
    public string? ExternalIp { get; set; }


    public bool TLSEnabled { get; set; } = false;

    public List<string> ListenIPAddresses { get; set; } = [];
}