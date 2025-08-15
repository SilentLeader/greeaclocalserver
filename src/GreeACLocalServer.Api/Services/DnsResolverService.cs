using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GreeACLocalServer.Api.Services;

public interface IDnsResolverService
{
    Task<string> ResolveDnsNameAsync(string ipAddress);
}

public class DnsResolverService(ILogger<DnsResolverService> logger) : IDnsResolverService
{
    private readonly ILogger<DnsResolverService> _logger = logger;

    public async Task<string> ResolveDnsNameAsync(string ipAddress)
    {
        try
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
                return ipAddress;
            }

            // Try to resolve DNS name
            var hostEntry = await Dns.GetHostEntryAsync(ip);
            
            if (!string.IsNullOrEmpty(hostEntry.HostName) && 
                !string.Equals(hostEntry.HostName, ipAddress, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Resolved DNS name for {IpAddress}: {DnsName}", ipAddress, hostEntry.HostName);
                return hostEntry.HostName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to resolve DNS name for {IpAddress}: {Error}", ipAddress, ex.Message);
        }

        // Fallback to IP address if DNS resolution fails
        return ipAddress;
    }
}
