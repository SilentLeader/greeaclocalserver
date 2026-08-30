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
    private readonly IOptionsMonitor<DeviceManagerOptions> _deviceManagerOptions;

    public ConfigService(
        IOptionsMonitor<ServerOptions> serverOptions,
        IOptionsMonitor<DeviceManagerOptions> deviceManagerOptions)
    {
        _serverOptions = serverOptions;
        _deviceManagerOptions = deviceManagerOptions;
    }

    public Task<ServerConfigResponse> GetServerConfigAsync(CancellationToken cancellationToken = default)
    {
        var serverConfig = _serverOptions.CurrentValue;
        var deviceManagerConfig = _deviceManagerOptions.CurrentValue;

        var response = new ServerConfigResponse
        {
            EnableManagement = serverConfig.EnableManagement,
            EnableUI = serverConfig.EnableUI,
            DeviceTimeoutMinutes = deviceManagerConfig.DeviceTimeoutMinutes
        };

        return Task.FromResult(response);
    }
}
