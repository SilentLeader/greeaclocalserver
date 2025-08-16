using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace GreeACLocalServer.Api.Options
{
    public class ForwardedHeadersConfiguration
    {
        public string ForwardedForHeaderName { get; set; } = "X-Forwarded-For";
        public string ForwardedProtoHeaderName { get; set; } = "X-Forwarded-Proto";
        public string ForwardedHostHeaderName { get; set; } = "X-Forwarded-Host";
        public bool RequireHeaderSymmetry { get; set; } = false;
        public List<string> KnownProxies { get; set; } = new();
        public List<string> KnownNetworks { get; set; } = new();
        public List<string> AllowedHosts { get; set; } = new();

        public void ApplyToForwardedHeadersOptions(ForwardedHeadersOptions options)
        {
            // Set forwarded headers
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | 
                                      ForwardedHeaders.XForwardedProto | 
                                      ForwardedHeaders.XForwardedHost;

            // Set header names
            options.ForwardedForHeaderName = ForwardedForHeaderName;
            options.ForwardedProtoHeaderName = ForwardedProtoHeaderName;
            options.ForwardedHostHeaderName = ForwardedHostHeaderName;
            options.RequireHeaderSymmetry = RequireHeaderSymmetry;

            // Clear and configure known proxies
            options.KnownProxies.Clear();
            foreach (var proxy in KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var ipAddress))
                {
                    options.KnownProxies.Add(ipAddress);
                }
            }

            // Clear and configure known networks
            options.KnownNetworks.Clear();
            foreach (var network in KnownNetworks)
            {
                if (TryParseNetwork(network, out var ipNetwork) && ipNetwork != null)
                {
                    options.KnownNetworks.Add(ipNetwork);
                }
            }

            // Configure allowed hosts
            options.AllowedHosts.Clear();
            foreach (var host in AllowedHosts)
            {
                options.AllowedHosts.Add(host);
            }
        }

        private static bool TryParseNetwork(string network, out Microsoft.AspNetCore.HttpOverrides.IPNetwork? ipNetwork)
        {
            ipNetwork = null;
            
            if (string.IsNullOrEmpty(network))
                return false;

            var parts = network.Split('/');
            if (parts.Length != 2)
                return false;

            if (!IPAddress.TryParse(parts[0], out var address))
                return false;

            if (!int.TryParse(parts[1], out var prefixLength))
                return false;

            try
            {
                ipNetwork = new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefixLength);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
