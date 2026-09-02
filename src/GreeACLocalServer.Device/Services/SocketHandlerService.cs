using System.Globalization;
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
    private int _tlsPort = ServerOption.TLS_PORT;
    private List<int> _plainPorts = [ServerOption.PORT];
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

            _plainPorts = _serverOptions.TcpPorts.Count > 0
                ? _serverOptions.TcpPorts.Distinct().ToList()
                : [ServerOption.PORT];
            _tlsPort = _serverOptions.TlsPort;
            var plainPorts = _plainPorts;

            var listenAddresses = _serverOptions.ListenIPAddresses.Any()
                ? _serverOptions.ListenIPAddresses.Select(IPAddress.Parse).ToList()
                : [IPAddress.Any];

            foreach (var address in listenAddresses)
            {
                foreach (var port in plainPorts)
                {
                    _servers.Add(new TcpListener(address, port));
                }
                if (_serverOptions.TLSEnabled)
                {
                    _servers.Add(new TcpListener(address, _tlsPort));
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

        if (IPAddress.TryParse(_serverOptions.ExternalIp, out var externalIp) && IPAddress.IsLoopback(externalIp))
        {
            _logger.LogWarning(
                "ExternalIp {ExternalIp} is a loopback address. AC devices on your network cannot reach the server here - "
                + "set GreeServer:ServerOptions:ExternalIp to this host's LAN IP (the discover response hands this value to the devices).",
                _serverOptions.ExternalIp);
        }

        _logger.LogInformation("Port(s) for AC Devices: {PORT}", string.Join(", ", _plainPorts));
        if (_serverOptions.TLSEnabled)
        {
            _logger.LogInformation("TLS Port for AC Devices: {TLS_PORT}", _tlsPort);
            _logger.LogInformation(
                "TLS protocols: {Protocols}",
                _serverOptions.AllowLegacyTlsProtocols ? "legacy (SSL3-TLS1.3)" : "TLS1.2+");
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
                var isTls = _serverOptions.TLSEnabled
                    && server.LocalEndpoint is IPEndPoint endpoint
                    && endpoint.Port == _tlsPort;

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
        var localPort = (client.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;

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

                    var authOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _tlsCertificate!,
                        ClientCertificateRequired = false,
                        EnabledSslProtocols = ResolveProtocols(_serverOptions.AllowLegacyTlsProtocols)
                    };

                    await sslStream.AuthenticateAsServerAsync(authOptions, _cancellationToken);
                    _logger.LogDebug("TLS handshake completed successfully");
                    clientStream = sslStream;
                }

                client.ReceiveTimeout = ServerOption.ReceiveTimeout;

                var idleTimeout = _serverOptions.IdleTimeoutSeconds > 0
                    ? TimeSpan.FromSeconds(_serverOptions.IdleTimeoutSeconds)
                    : Timeout.InfiniteTimeSpan;

                // GREE devices always open with a '{' JSON line. Some newer firmware
                // additionally opens a connection carrying a binary "fg" telemetry
                // frame, and a stray TLS ClientHello can land on a plaintext port.
                // Peek the first byte so binary is never fed to the line reader - a
                // 0x0A byte inside a binary frame is not a message boundary.
                var firstByte = new byte[1];
                int firstRead;
                try
                {
                    firstRead = await ReadWithIdleTimeoutAsync(clientStream, firstByte, idleTimeout, _cancellationToken);
                }
                catch (OperationCanceledException) when (!_cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Client sent no data within {IdleSeconds}s, closing connection", idleTimeout.TotalSeconds);
                    return;
                }

                if (firstRead == 0)
                {
                    return; // peer closed before sending anything
                }

                if (firstByte[0] != (byte)'{')
                {
                    await HandleUnrecognizedConnectionAsync(
                        clientStream, firstByte[0], clientIPAddress, localPort, isTLS, idleTimeout);
                    return;
                }

                var jsonStream = new PrefixedReadStream(firstByte.AsMemory(0, 1), clientStream);

                using (var sReader = new StreamReader(jsonStream, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
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
                                IPAddress = clientIPAddress,
                                Port = localPort,
                                IsTls = isTLS
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

    /// <summary>
    /// Reads once from <paramref name="stream"/>, bounding the wait with
    /// <paramref name="idleTimeout"/> (a linked CTS, because the socket receive
    /// timeout does not apply to <see cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/>)
    /// and with the server shutdown token. An idle timeout surfaces as
    /// <see cref="OperationCanceledException"/> while <paramref name="shutdownToken"/>
    /// is not cancelled.
    /// </summary>
    private static async Task<int> ReadWithIdleTimeoutAsync(
        Stream stream, Memory<byte> buffer, TimeSpan idleTimeout, CancellationToken shutdownToken)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        if (idleTimeout > TimeSpan.Zero)
        {
            readCts.CancelAfter(idleTimeout);
        }

        return await stream.ReadAsync(buffer, readCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a connection whose first byte is not <c>{</c>, i.e. it is not the
    /// GREE JSON line protocol (a binary "fg" telemetry frame, a misdirected TLS
    /// ClientHello, ...). The bytes are drained raw - never split on newlines -
    /// bounded by <see cref="ServerOption.MaxUnknownFrameBytes"/> and a short drain
    /// window, logged once, and optionally written to
    /// <see cref="ServerOptions.UnknownFrameCapturePath"/> for offline analysis.
    /// </summary>
    private async Task HandleUnrecognizedConnectionAsync(
        Stream stream,
        byte firstByte,
        string? clientIpAddress,
        int localPort,
        bool isTls,
        TimeSpan idleTimeout)
    {
        var capturePath = _serverOptions.UnknownFrameCapturePath;
        var saving = !string.IsNullOrWhiteSpace(capturePath);

        var drainTimeout = TimeSpan.FromSeconds(ServerOption.UnknownFrameDrainSeconds);
        if (idleTimeout > TimeSpan.Zero && idleTimeout < drainTimeout)
        {
            drainTimeout = idleTimeout;
        }

        // First bytes are always kept for the log line; the full payload only when
        // it is going to be written to disk.
        var header = new byte[64];
        header[0] = firstByte;
        var headerLen = 1;

        using var full = saving ? new MemoryStream() : null;
        full?.WriteByte(firstByte);

        long total = 1;
        var chunk = new byte[4096];
        try
        {
            while (_isRunning && total < ServerOption.MaxUnknownFrameBytes)
            {
                int n;
                try
                {
                    n = await ReadWithIdleTimeoutAsync(stream, chunk, drainTimeout, _cancellationToken);
                }
                catch (OperationCanceledException) when (!_cancellationToken.IsCancellationRequested)
                {
                    break; // no further bytes within the drain window
                }

                if (n == 0)
                {
                    break; // peer closed
                }

                if (headerLen < header.Length)
                {
                    var take = Math.Min(n, header.Length - headerLen);
                    Array.Copy(chunk, 0, header, headerLen, take);
                    headerLen += take;
                }

                if (full is not null)
                {
                    var room = ServerOption.MaxUnknownFrameBytes - total;
                    full.Write(chunk, 0, (int)Math.Min(n, room));
                }

                total += n;
            }
        }
        catch (IOException)
        {
            // Connection reset mid-drain - nothing more to collect.
        }
        catch (OperationCanceledException)
        {
            // Server shutting down.
        }

        var headerHex = Convert.ToHexString(header.AsSpan(0, headerLen));

        string? savedFile = null;
        if (saving && full is not null)
        {
            savedFile = await TrySaveUnknownFrameAsync(capturePath!, full.ToArray(), clientIpAddress, localPort, isTls);
        }

        _logger.LogInformation(
            "Unrecognized non-JSON connection from {IpAddress} on port {Port} (TLS={IsTls}): {ByteCount} byte(s), header {HeaderHex}{Saved}",
            clientIpAddress,
            localPort,
            isTls,
            total,
            headerHex,
            savedFile is null ? string.Empty : $", saved to {savedFile}");
    }

    private int _unknownFrameFilesWritten;

    private async Task<string?> TrySaveUnknownFrameAsync(
        string directory, byte[] payload, string? clientIpAddress, int localPort, bool isTls)
    {
        if (Interlocked.Increment(ref _unknownFrameFilesWritten) > ServerOption.MaxUnknownFrameCaptureFiles)
        {
            _logger.LogWarning(
                "Unknown-frame capture limit ({Limit}) reached this run; further payloads are logged but not saved",
                ServerOption.MaxUnknownFrameCaptureFiles);
            return null;
        }

        try
        {
            Directory.CreateDirectory(directory);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            var ipPart = string.IsNullOrEmpty(clientIpAddress)
                ? "unknown"
                : clientIpAddress.Replace(':', '-').Replace('.', '-');
            var baseName = $"gree-unknown_{stamp}_{ipPart}_{localPort}_{Guid.NewGuid():N}";
            var binPath = Path.Combine(directory, baseName + ".bin");

            await File.WriteAllBytesAsync(binPath, payload, CancellationToken.None).ConfigureAwait(false);

            var sidecar = new StringBuilder()
                .Append("received-utc : ").AppendLine(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                .Append("remote       : ").AppendLine(clientIpAddress ?? "(unknown)")
                .Append("local-port   : ").AppendLine(localPort.ToString(CultureInfo.InvariantCulture))
                .Append("tls          : ").AppendLine(isTls ? "true" : "false")
                .Append("bytes        : ").AppendLine(payload.Length.ToString(CultureInfo.InvariantCulture))
                .Append("first-hex    : ").AppendLine(Convert.ToHexString(payload.AsSpan(0, Math.Min(128, payload.Length))));
            AppendDecodedHeader(sidecar, payload);

            await File.WriteAllTextAsync(Path.Combine(directory, baseName + ".txt"), sidecar.ToString(), CancellationToken.None)
                .ConfigureAwait(false);

            return binPath;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(e, "Failed to save unknown-frame capture to {Directory}", directory);
            return null;
        }
    }

    /// <summary>
    /// Best-effort decode of the binary "fg" frame header observed on some newer
    /// firmware: magic <c>66 67</c>, then a 2-byte type, then the device MAC. The
    /// body is left as opaque (AES-ECB with an as-yet-unknown key). Purely
    /// informational for the sidecar file.
    /// </summary>
    private static void AppendDecodedHeader(StringBuilder sb, byte[] p)
    {
        if (p.Length < 10 || p[0] != (byte)'f' || p[1] != (byte)'g')
        {
            return;
        }

        sb.Append("fg-magic     : ").AppendLine("66 67 (\"fg\")");
        sb.Append("fg-type      : ").AppendLine(Convert.ToHexString(p.AsSpan(2, 2)));
        sb.Append("fg-mac       : ").AppendLine(Convert.ToHexString(p.AsSpan(4, 6)).ToLowerInvariant());
    }

    /// <summary>
    /// Resolves the enabled TLS protocol set for the device listener. When
    /// <paramref name="allowLegacy"/> is true the legacy SSL3 / TLS 1.0 / TLS 1.1
    /// protocols are included for old AC firmware; otherwise only TLS 1.2 / 1.3.
    /// </summary>
    internal static SslProtocols ResolveProtocols(bool allowLegacy)
    {
        if (!allowLegacy)
        {
            return SslProtocols.Tls12 | SslProtocols.Tls13;
        }

#pragma warning disable CS0618, SYSLIB0039 // legacy protocols are intentionally opt-in
        return SslProtocols.Ssl3
             | SslProtocols.Tls
             | SslProtocols.Tls11
             | SslProtocols.Tls12
             | SslProtocols.Tls13;
#pragma warning restore CS0618, SYSLIB0039
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