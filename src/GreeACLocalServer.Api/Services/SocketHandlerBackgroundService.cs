using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace GreeACLocalServer.Api.Services
{
    public class SocketHandlerBackgroundService(SocketHandlerService socketHandlerService) : BackgroundService
    {
        private readonly SocketHandlerService _socketHandlerService = socketHandlerService;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(() => _socketHandlerService.Start(), stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _socketHandlerService.Stop();
            await base.StopAsync(cancellationToken);
        }
    }
}
