using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace GreeACHeartBeatServer.Api.Services
{
    public class SocketHandlerBackgroundService : BackgroundService
    {
        private readonly SocketHandlerService _socketHandlerService;
        public SocketHandlerBackgroundService(SocketHandlerService socketHandlerService)
        {
            _socketHandlerService = socketHandlerService;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => _socketHandlerService.Start(), stoppingToken);
        }
    }
}
