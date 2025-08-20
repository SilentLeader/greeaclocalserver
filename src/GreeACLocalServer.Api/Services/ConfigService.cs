using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Shared.DTOs;
using GreeACLocalServer.Shared.Interfaces;
using Microsoft.Extensions.Options;

namespace GreeACLocalServer.Api.Services;

public class ConfigService : IConfigService
{
    private readonly IOptionsMonitor<ServerOptions> _serverOptions;

    public ConfigService(IOptionsMonitor<ServerOptions> serverOptions)
    {
        _serverOptions = serverOptions;
    }

    public Task<ServerConfigResponse> GetServerConfigAsync(CancellationToken cancellationToken = default)
    {
        var serverConfig = _serverOptions.CurrentValue;
        
        var response = new ServerConfigResponse
        {
            EnableManagement = serverConfig.EnableManagement,
            EnableUI = serverConfig.EnableUI
        };

        return Task.FromResult(response);
    }
}
