using System.Collections.Generic;

namespace GreeACLocalServer.Api.Options
{
    public class ServerOptions
    {
        public int Port { get; set; }
        public string? DomainName { get; set; }
        public string? ExternalIp { get; set; }
        public List<string> ListenIPAddresses { get; set; } = new();
        public string? CryptoKey { get; set; }
        public bool EnableUI { get; set; } = true;
    }
}
