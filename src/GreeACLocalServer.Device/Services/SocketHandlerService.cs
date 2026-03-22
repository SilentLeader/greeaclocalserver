using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.ValueObjects;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Device.Services;


internal class SocketHandlerService(
    IMessageHandlerService greeHandler,
    IOptions<ServerOptions> serverOptions,
    IDeviceEventPublisher deviceEventPublisher,
    ILogger<SocketHandlerService> logger) : ISocketHandlerService
{
    private readonly ConcurrentBag<TcpListener> _servers = [];
    private volatile bool _isRunning;
    private readonly IMessageHandlerService _greeHandler = greeHandler;
    private readonly IDeviceEventPublisher _deviceEventPublisher = deviceEventPublisher;

    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly ILogger<SocketHandlerService> _logger = logger;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken _cancellationToken => _cancellationTokenSource.Token;

    public void Start()
    {
        if (_serverOptions.ListenIPAddresses.Any())
        {
            foreach (var ip in _serverOptions.ListenIPAddresses)
            {
                _servers.Add(new TcpListener(IPAddress.Parse(ip), ServerOption.HTTP_PORT));
            }
        }
        else
        {
            _servers.Add(new TcpListener(IPAddress.Any, ServerOption.HTTP_PORT));
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
        _logger.LogInformation("Port for AC Devices: {HTTP_PORT}", ServerOption.HTTP_PORT);
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();

        foreach (var server in _servers)
        {
            server.Stop();
        }

        _logger.LogInformation("Server stopped");
    }

    private async Task AcceptClientsLoop(TcpListener server)
    {
        while (_isRunning)
        {
            try
            {
                var newClient = await server.AcceptTcpClientAsync(_cancellationToken);
                if (_cancellationToken.IsCancellationRequested)
                {
                    continue;
                }
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

                var response = _greeHandler.GetResponse(data);
                isClientConnected = response.KeepAlive;

                if (!string.IsNullOrEmpty(response.Data))
                {
                    sWriter.WriteLine(response.Data);
                    sWriter.Flush();
                }

                if (!string.IsNullOrEmpty(response.MacAddress))
                {
                    _deviceEventPublisher.DeviceConnected(new DeviceConnectedMessage
                    {
                        MacAddress = response.MacAddress,
                        IPAddress = clientIPAddress
                    });
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