using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GreeACHeartBeatServer.Api.Responses;
using Microsoft.Extensions.Options;
using GreeACHeartBeatServer.Api.Options;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GreeACHeartBeatServer.Api.Services
{
    public class SocketHandlerService
    {
        private List<TcpListener> _servers = [];
        private bool _isRunning;
        private readonly MessageHandlerService _greeHandler;
        private readonly ServerOptions _serverOptions;
        private readonly ILogger<SocketHandlerService> _logger;

        public SocketHandlerService(MessageHandlerService greeHandler, IOptions<ServerOptions> serverOptions, ILogger<SocketHandlerService> logger)
        {
            _greeHandler = greeHandler;
            _serverOptions = serverOptions.Value;
            _logger = logger;
        }
 
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
            }
 
            _isRunning = true;

            _logger.LogInformation("Gree AC server started");
            _logger.LogInformation("Domainname for AC Devices: {DomainName}", _serverOptions.DomainName);
            _logger.LogInformation("IP Address for AC Devices: {ExternalIp}", _serverOptions.ExternalIp);

            Parallel.ForEach(_servers, server =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var newClient = server.AcceptTcpClient();
                        var clientHandler = new Thread(() => HandleClient(newClient));
                        clientHandler.Start();
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.Interrupted)
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
            });
        }
 
        private void HandleClient(TcpClient client)        
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

                    while (isClientConnected)
                    {
                        var data = sReader.ReadLine();

                        GreeHandlerResponse response = _greeHandler.GetResponse(data);
                        isClientConnected = response.KeepAlive;

                        if (response.Data != string.Empty)
                        {
                            sWriter.WriteLine(response.Data);
                            sWriter.Flush();
                        }
                    }

                    _logger.LogInformation("Connection close.");

                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unhandled error");
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