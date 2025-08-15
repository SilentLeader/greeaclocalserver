using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using GreeACLocalServer.Api.Models;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Api.Options;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Collections.Concurrent;
using GreeACLocalServer.Api.Services;
using GreeHandlerResponse = GreeACLocalServer.Api.Models.GreeHandlerResponse;

namespace GreeACLocalServer.Api.Services
{

    public class SocketHandlerService(MessageHandlerService greeHandler, IOptions<ServerOptions> serverOptions, ILogger<SocketHandlerService> logger, IInternalDeviceManagerService deviceManager)
    {
        private readonly ConcurrentBag<TcpListener> _servers = new();
        private volatile bool _isRunning;
        private readonly MessageHandlerService _greeHandler = greeHandler;
        private readonly ServerOptions _serverOptions = serverOptions.Value;
        private readonly ILogger<SocketHandlerService> _logger = logger;
        private readonly IInternalDeviceManagerService _deviceManager = deviceManager;

        public void Start()
        {
            if (_serverOptions.ListenIPAddresses.Any())
            {
                foreach (var ip in _serverOptions.ListenIPAddresses)
                {
                    _servers.Add(new TcpListener(IPAddress.Parse(ip), _serverOptions.Port));
                }
            }
            else
            {
                _servers.Add(new TcpListener(IPAddress.Any, _serverOptions.Port));
            }

            foreach (var server in _servers)
            {
                server.Start();
                Task.Run(() => AcceptClientsLoop(server));
            }

            _isRunning = true;

            _logger.LogInformation("Gree AC server started");
            _logger.LogInformation("Domainname for AC Devices: {DomainName}", _serverOptions.DomainName);
            _logger.LogInformation("IP Address for AC Devices: {ExternalIp}", _serverOptions.ExternalIp);
        }

        private async Task AcceptClientsLoop(TcpListener server)
        {
            while (_isRunning)
            {
                try
                {
                    var newClient = await server.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(newClient));
                }
                catch (ObjectDisposedException)
                {
                    // Listener stopped, exit loop
                    break;
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.Interrupted || e.SocketErrorCode == SocketError.OperationAborted)
                    {
                        break;
                    }
                    else
                    {
                        _logger.LogError(e, "Socket Error");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Connections Error");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var clientIPAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
            using (LogContext.PushProperty("ClientIPAddress", clientIPAddress))
            {
                try
                {
                    using var sWriter = new StreamWriter(client.GetStream(), Encoding.ASCII);
                    using var sReader = new StreamReader(client.GetStream(), Encoding.ASCII);
                    client.ReceiveTimeout = 5 * 60000; // 5 minutes
                    bool isClientConnected = true;

                    while (isClientConnected && _isRunning)
                    {
                        var data = sReader.ReadLine();
                        if (data == null) break;

                        GreeHandlerResponse response = _greeHandler.GetResponse(data);
                        isClientConnected = response.KeepAlive;

                        if (!string.IsNullOrEmpty(response.Data))
                        {
                            sWriter.WriteLine(response.Data);
                            sWriter.Flush();
                        }

                        if (!string.IsNullOrEmpty(response.MacAddress))
                        {
                            await _deviceManager.UpdateOrAddAsync(response.MacAddress, clientIPAddress ?? "");
                        }
                    }

                    _logger.LogDebug("Connection close.");
                }
                catch (IOException e)
                {
                    _logger.LogWarning(e, "Client connection closed or timed out");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled error");
                }
                finally
                {
                    client.Close();
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;

            foreach (var server in _servers)
                server.Stop();

            _logger.LogInformation("Server stopped");
        }
    }
}