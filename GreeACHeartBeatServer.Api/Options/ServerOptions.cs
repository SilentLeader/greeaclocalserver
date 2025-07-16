using System.Collections.Generic;

namespace GreeACHeartBeatServer.Api.Options
{
    public class ServerOptions
    {
        public int Port { get; set; }
        public string DomainName { get; set; }
        public string ExternalIp { get; set; }
        public List<string> ListenIPAddresses { get; set; } = [];
        public string CryptoKey { get; set; }
    }
}
