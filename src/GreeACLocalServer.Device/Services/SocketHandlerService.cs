using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.ValueObjects;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Device.Models;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Serilog.Context;

namespace GreeACLocalServer.Device.Services;


internal class SocketHandlerService(
    IMessageHandlerService greeHandler,
    IOptions<ServerOptions> serverOptions,
    IDeviceEventPublisher deviceEventPublisher,
    ICryptoService cryptoService,
    ILogger<SocketHandlerService> logger) : ISocketHandlerService
{
    private readonly ConcurrentBag<TcpListener> _servers = [];
    private volatile bool _isRunning;
    private X509Certificate2? _tlsCertificate;

    private readonly IMessageHandlerService _greeHandler = greeHandler;
    private readonly IDeviceEventPublisher _deviceEventPublisher = deviceEventPublisher;
    private readonly ICryptoService _cryptoService = cryptoService;
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly ILogger<SocketHandlerService> _logger = logger;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken _cancellationToken => _cancellationTokenSource.Token;

    public void Start()
    {
        _logger.LogDebug("Gree AC server starting...");
        if (_serverOptions.TLSEnabled)
        {
            _logger.LogDebug("GREE device TLS listener certificate loading...");
            _tlsCertificate = _cryptoService.GetCertificate(_serverOptions.DomainName);
            _logger.LogDebug("GREE device TLS listener certificate loaded. (Common name: {Subject})", _tlsCertificate.Subject);
        }

        if (_serverOptions.ListenIPAddresses.Any())
        {
            foreach (var ip in _serverOptions.ListenIPAddresses)
            {
                _servers.Add(new TcpListener(IPAddress.Parse(ip), ServerOption.PORT));
                if (_serverOptions.TLSEnabled)
                {
                    _servers.Add(new TcpListener(IPAddress.Parse(ip), ServerOption.TLS_PORT));
                }
            }
        }
        else
        {
            _servers.Add(new TcpListener(IPAddress.Any, ServerOption.PORT));
            if (_serverOptions.TLSEnabled)
            {
                _servers.Add(new TcpListener(IPAddress.Any, ServerOption.TLS_PORT));
            }
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
        _logger.LogInformation("Port for AC Devices: {PORT}", ServerOption.PORT);
        if (_serverOptions.TLSEnabled)
        {
            _logger.LogInformation("TLS Port for AC Devices: {TLS_PORT}", ServerOption.TLS_PORT);
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        _logger.LogDebug("Gree AC server stopping...");

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
                var isTls = server.LocalEndpoint is IPEndPoint { Port: ServerOption.TLS_PORT };
                _ = Task.Run(() => HandleClientAsync(newClient, isTls));
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

    private async Task HandleClientAsync(TcpClient client, bool isTLS)
    {
        var clientIPAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
        
        using (LogContext.PushProperty("ConnectionId", Guid.NewGuid().ToString("N")[..8]))
        {
            _logger.LogDebug("Client connected from {IpAddress}", clientIPAddress);

            try
            {
                Stream clientStream = client.GetStream();

                if (isTLS)
                {
                    var sslStream = new SslStream(client.GetStream(), false, ValidateCertificate);
                    await sslStream.AuthenticateAsServerAsync(_tlsCertificate!);
                    _logger.LogDebug("TLS handshake completed successfully");
                    clientStream = sslStream;
                }

                using var sWriter = new StreamWriter(clientStream, Encoding.ASCII);
                using var sReader = new StreamReader(clientStream, Encoding.ASCII);
                client.ReceiveTimeout = ServerOption.ReceiveTimeout;
                bool isClientConnected = true;

                while (isClientConnected && _isRunning)
                {
                    var data = sReader.ReadLine();
                    if (data == null)
                    {
                        break;
                    }

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
            }
            catch (IOException e)
            {
                _logger.LogWarning(e, "Client connection closed or timed out");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled error in client handler");
            }
            finally
            {
                _logger.LogDebug("Connection closed");
                client.Close();
            }
        }
    }

    private bool ValidateCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // Accept all certificate
        return true;
    }
}