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
using System.Security.Authentication;

namespace GreeACLocalServer.Device.Services;


internal class SocketHandlerService(
    IMessageHandlerService greeHandler,
    IOptions<ServerOptions> serverOptions,
    IDeviceEventPublisher deviceEventPublisher,
    ICryptoService cryptoService,
    ILogger<SocketHandlerService> logger) : ISocketHandlerService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ConcurrentBag<TcpListener> _servers = [];
    private readonly List<Task> _acceptLoops = [];
    private readonly object _lifecycleLock = new();
    private volatile bool _isRunning;
    private X509Certificate2? _tlsCertificate;
    private SemaphoreSlim? _connectionLimiter;

    private readonly IMessageHandlerService _greeHandler = greeHandler;
    private readonly IDeviceEventPublisher _deviceEventPublisher = deviceEventPublisher;
    private readonly ICryptoService _cryptoService = cryptoService;
    private readonly ServerOptions _serverOptions = serverOptions.Value;
    private readonly ILogger<SocketHandlerService> _logger = logger;

    private CancellationTokenSource _cancellationTokenSource = new();
    private CancellationToken _cancellationToken => _cancellationTokenSource.Token;

    public void Start()
    {
        lock (_lifecycleLock)
        {
            StartCore();
        }
    }

    private void StartCore()
    {
        if (_isRunning)
        {
            _logger.LogDebug("Gree AC server already running, ignoring Start()");
            return;
        }

        _logger.LogDebug("Gree AC server starting...");

        // A previous Stop() cancelled the token; a CTS cannot be reset, so make a
        // fresh one before this run.
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        var maxConnections = _serverOptions.MaxConcurrentConnections > 0
            ? _serverOptions.MaxConcurrentConnections
            : int.MaxValue;
        _connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);
        _acceptLoops.Clear();

        try
        {
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

            _isRunning = true;

            foreach (var server in _servers)
            {
                server.Start();
                _acceptLoops.Add(Task.Run(() => AcceptClientsLoop(server)));
            }
        }
        catch
        {
            // Roll back partial state so a later Start() (or the host retrying) is not
            // blocked by the `if (_isRunning) return` guard on a half-initialised server.
            _isRunning = false;
            _cancellationTokenSource.Cancel();
            foreach (var server in _servers)
            {
                try { server.Stop(); } catch { /* best effort */ }
            }
            _servers.Clear();
            _acceptLoops.Clear();
            _tlsCertificate?.Dispose();
            _tlsCertificate = null;
            _connectionLimiter = null;
            throw;
        }

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
        lock (_lifecycleLock)
        {
            if (!_isRunning)
            {
                _logger.LogDebug("Gree AC server not running, ignoring Stop()");
                return;
            }

            _isRunning = false;
            _cancellationTokenSource.Cancel();
            _logger.LogDebug("Gree AC server stopping...");

            foreach (var server in _servers)
            {
                server.Stop();
            }
            _servers.Clear();

            try
            {
                Task.WhenAll(_acceptLoops).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Accept loop(s) faulted during shutdown");
            }
            _acceptLoops.Clear();

            // Not disposed on purpose: a client handler that is still unwinding will
            // call _connectionLimiter.Release() from its finally block. Release() on an
            // orphaned (but not disposed) SemaphoreSlim is harmless; Release() on a
            // disposed one throws. Nobody touches AvailableWaitHandle, so skipping
            // Dispose() leaks nothing meaningful.
            _connectionLimiter = null;

            _tlsCertificate?.Dispose();
            _tlsCertificate = null;

            _logger.LogInformation("Server stopped");
        }
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
                    newClient.Dispose();
                    continue;
                }
                var isTls = server.LocalEndpoint is IPEndPoint { Port: ServerOption.TLS_PORT };

                var limiter = _connectionLimiter;
                if (limiter is not null && !await limiter.WaitAsync(TimeSpan.Zero, _cancellationToken))
                {
                    _logger.LogWarning(
                        "Concurrent connection limit ({Limit}) reached, dropping client {IpAddress}",
                        _serverOptions.MaxConcurrentConnections,
                        (newClient.Client.RemoteEndPoint as IPEndPoint)?.Address);
                    newClient.Close();
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(newClient, isTls, limiter));
            }
            catch (ObjectDisposedException)
            {
                // Listener stopped, exit loop
                break;
            }
            catch (OperationCanceledException)
            {
                // Stop() cancelled the token, exit loop
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

    private async Task HandleClientAsync(TcpClient client, bool isTLS, SemaphoreSlim? connectionLimiter)
    {
        var clientIPAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();

        using (LogContext.PushProperty("ConnectionId", Guid.NewGuid().ToString("N")[..8]))
        {
            _logger.LogDebug("Client connected from {IpAddress}", clientIPAddress);

            SslStream? sslStream = null;
            try
            {
                Stream clientStream = client.GetStream();

                if (isTLS)
                {
                    sslStream = new SslStream(clientStream, false, ValidateCertificate);

                    // Support legacy ssl protocols
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable SYSLIB0039 // Type or member is obsolete

                    var authOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _tlsCertificate!,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = SslProtocols.Ssl3 |
                                              SslProtocols.Tls |
                                              SslProtocols.Tls11 |
                                              SslProtocols.Tls12 |
                                              SslProtocols.Tls13
                    };
#pragma warning restore SYSLIB0039 // Type or member is obsolete
#pragma warning restore CS0618 // Type or member is obsolete

                    await sslStream.AuthenticateAsServerAsync(authOptions, _cancellationToken);
                    _logger.LogDebug("TLS handshake completed successfully");
                    clientStream = sslStream;
                }

                client.ReceiveTimeout = ServerOption.ReceiveTimeout;

                var idleTimeout = _serverOptions.IdleTimeoutSeconds > 0
                    ? TimeSpan.FromSeconds(_serverOptions.IdleTimeoutSeconds)
                    : Timeout.InfiniteTimeSpan;

                using (var sReader = new StreamReader(clientStream, Utf8NoBom, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                using (var sWriter = new StreamWriter(clientStream, Utf8NoBom, bufferSize: 1024, leaveOpen: true) { AutoFlush = false, NewLine = "\n" })
                {
                    bool isClientConnected = true;

                    while (isClientConnected && _isRunning)
                    {
                        var read = await ClientLineReader.ReadLineAsync(
                            sReader, idleTimeout, ServerOption.MaxLineLength, _cancellationToken);

                        if (read.Outcome == ClientLineReader.ReadOutcome.Closed)
                        {
                            break;
                        }
                        if (read.Outcome == ClientLineReader.ReadOutcome.IdleTimeout)
                        {
                            _logger.LogDebug("Client idle for {IdleSeconds}s, closing connection", idleTimeout.TotalSeconds);
                            break;
                        }
                        if (read.Outcome == ClientLineReader.ReadOutcome.LineTooLong)
                        {
                            _logger.LogWarning(
                                "Client sent a line longer than {MaxLineLength} characters, closing connection",
                                ServerOption.MaxLineLength);
                            break;
                        }

                        var data = read.Line!;

                        var response = _greeHandler.GetResponse(data, isTLS);
                        isClientConnected = response.KeepAlive;

                        if (!string.IsNullOrEmpty(response.Data))
                        {
                            await sWriter.WriteLineAsync(response.Data.AsMemory(), _cancellationToken);
                            await sWriter.FlushAsync(_cancellationToken);
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
                } // flush + dispose the readers/writers while the stream is still open
            }
            catch (OperationCanceledException)
            {
                // Server shutting down.
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
                sslStream?.Dispose();
                client.Close();
                connectionLimiter?.Release();
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