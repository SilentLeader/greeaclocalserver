using System.Threading;
using System.Threading.Tasks;
using GreeACLocalServer.Shared.DTOs;

namespace GreeACLocalServer.Shared.Interfaces;

public interface IConfigService
{
    Task<ServerConfigResponse> GetServerConfigAsync(CancellationToken cancellationToken = default);
}
