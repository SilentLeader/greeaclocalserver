using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Responses;

namespace GreeACLocalServer.DeviceEmulator;

/// <summary>
/// Plays the "real device" side of the inbound GREE protocol
/// (<c>GreeACLocalServer.Device.Services.SocketHandlerService</c> /
/// <c>MessageHandlerService</c>): opens a TCP/TLS connection to the server,
/// logs in (<c>devLogin</c>), and keeps the connection alive with periodic
/// heartbeats so the device shows up as "online" in the UI.
/// </summary>
public sealed class InboundClient(EmulatedDeviceState state, ICryptoService crypto, InboundClientOptions options)
{
    private volatile bool _desiredConnected = true;
    private CancellationTokenSource? _sessionCts;

    public bool IsConnected { get; private set; }

    /// <summary>Requests the client (re)connect and resume the login/heartbeat loop.</summary>
    public void RequestConnect() => _desiredConnected = true;

    /// <summary>
    /// Requests the client close its connection - simulates the AC unit being
    /// powered off or losing network. Takes effect immediately even mid-heartbeat-wait.
    /// </summary>
    public void RequestDisconnect()
    {
        _desiredConnected = false;
        _sessionCts?.Cancel();
    }

    public async Task RunAsync(CancellationToken shutdownToken)
    {
        await RunDiscoverOnceAsync(shutdownToken).ConfigureAwait(false);

        var backoff = TimeSpan.FromSeconds(1);
        while (!shutdownToken.IsCancellationRequested)
        {
            if (!_desiredConnected)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), shutdownToken).ConfigureAwait(false);
                continue;
            }

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
            _sessionCts = sessionCts;
            try
            {
                await RunSessionAsync(sessionCts.Token).ConfigureAwait(false);
                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // A user-requested disconnect cancelled the session token; not an error.
                ConsoleLog.Info("Disconnected.");
            }
            catch (Exception ex)
            {
                if (_desiredConnected)
                {
                    ConsoleLog.Warn($"Connection lost/failed: {ex.Message}. Retrying in {backoff.TotalSeconds:0}s");
                    try
                    {
                        await Task.Delay(backoff, shutdownToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
                }
            }
            finally
            {
                IsConnected = false;
                _sessionCts = null;
            }
        }
    }

    private async Task RunSessionAsync(CancellationToken ct)
    {
        var (client, stream, ssl) = await OpenConnectionAsync(ct).ConfigureAwait(false);
        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

            await LoginAsync(reader, writer, ct).ConfigureAwait(false);

            ConsoleLog.Info($"Connected and logged in to {options.Host}:{options.Port} (mac={state.Mac})");
            IsConnected = true;

            // Mirrors a real device syncing the clock once after login; response is informational.
            await WriteLineAsync(writer, new { t = "tm" }, ct).ConfigureAwait(false);
            await ReadLineWithTimeoutAsync(reader, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

            while (_desiredConnected)
            {
                await Task.Delay(options.HeartbeatInterval, ct).ConfigureAwait(false);
                if (!_desiredConnected)
                {
                    break;
                }

                await WriteLineAsync(writer, new { t = "hb", mac = state.Mac }, ct).ConfigureAwait(false);
                _ = await ReadLineWithTimeoutAsync(reader, TimeSpan.FromSeconds(10), ct).ConfigureAwait(false)
                    ?? throw new IOException("No heartbeat response from server");
            }
        }
        finally
        {
            ssl?.Dispose();
            client.Dispose();
        }
    }

    private async Task LoginAsync(StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        var obscuredMac = MacObfuscation.Obscure(state.Mac);
        var innerLogin = JsonSerializer.Serialize(new { t = "devLogin", mac = obscuredMac });
        var encryptedLogin = crypto.Encrypt(innerLogin);

        await WriteLineAsync(writer, new { t = "pack", mac = state.Mac, pack = encryptedLogin }, ct).ConfigureAwait(false);

        var loginLine = await ReadLineWithTimeoutAsync(reader, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false)
            ?? throw new IOException("No devLogin response from server");

        using var outer = JsonDocument.Parse(loginLine);
        if (!outer.RootElement.TryGetProperty("pack", out var packElement) || packElement.GetString() is not { Length: > 0 } packB64)
        {
            throw new IOException($"devLogin response missing pack payload: {loginLine}");
        }

        var decrypted = crypto.Decrypt(packB64);
        var login = JsonSerializer.Deserialize<LoginResponse>(decrypted);
        if (login is null || login.ResponseType != "loginRes" || login.ResponseCode != 200)
        {
            throw new IOException($"devLogin rejected: {loginLine}");
        }
    }

    private async Task RunDiscoverOnceAsync(CancellationToken ct)
    {
        try
        {
            var (client, stream, ssl) = await OpenConnectionAsync(ct).ConfigureAwait(false);
            try
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\n" };

                await WriteLineAsync(writer, new { t = "dis", mac = state.Mac }, ct).ConfigureAwait(false);
                var line = await ReadLineWithTimeoutAsync(reader, TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                if (line is null)
                {
                    ConsoleLog.Warn("Discover: no response from server (non-fatal)");
                    return;
                }

                using var outer = JsonDocument.Parse(line);
                var packB64 = outer.RootElement.GetProperty("pack").GetString()!;
                var decrypted = crypto.Decrypt(packB64);
                var discover = JsonSerializer.Deserialize<DiscoverResponse>(decrypted);
                ConsoleLog.Info($"Discover -> server reports host={discover?.ServerHost} ip={discover?.HostOrIpAddress} tcpPort={discover?.TcpPort}");
            }
            finally
            {
                ssl?.Dispose();
                client.Dispose();
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Warn($"Discover request failed (non-fatal): {ex.Message}");
        }
    }

    private async Task<(TcpClient Client, Stream Stream, SslStream? Ssl)> OpenConnectionAsync(CancellationToken ct)
    {
        var client = new TcpClient();
        await client.ConnectAsync(options.Host, options.Port, ct).ConfigureAwait(false);

        Stream stream = client.GetStream();
        SslStream? ssl = null;
        if (options.UseTls)
        {
            ssl = new SslStream(stream, leaveInnerStreamOpen: false, ValidateServerCertificate);
#pragma warning disable CS0618, SYSLIB0039 // legacy protocols are intentionally opt-in, to emulate old AC firmware
            var protocols = options.AllowLegacyTls
                ? SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13
                : SslProtocols.Tls12 | SslProtocols.Tls13;
#pragma warning restore CS0618, SYSLIB0039

            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = options.Host,
                EnabledSslProtocols = protocols,
            }, ct).ConfigureAwait(false);
            stream = ssl;
        }

        return (client, stream, ssl);
    }

    /// <summary>The server uses an auto-generated self-signed cert for old AC firmware; accept it unconditionally, like a real device would.</summary>
    private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors) => true;

    private static Task WriteLineAsync(StreamWriter writer, object payload, CancellationToken ct)
        => writer.WriteLineAsync(JsonSerializer.Serialize(payload).AsMemory(), ct);

    private static async Task<string?> ReadLineWithTimeoutAsync(StreamReader reader, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            return await reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
