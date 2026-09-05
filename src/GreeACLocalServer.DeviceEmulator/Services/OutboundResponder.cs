using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Responses;
using GreeACLocalServer.DeviceEmulator.Models;

namespace GreeACLocalServer.DeviceEmulator.Services;

/// <summary>
/// Plays the "real device" side of the outbound GREE protocol
/// (<c>GreeACLocalServer.Device.Services.DeviceControllerService</c>): a UDP
/// listener on port 7000 that answers the server's <c>scan -&gt; bind -&gt; pack</c>
/// handshake, so the "Device Config" page and runtime-state/firmware polling work
/// against the emulator exactly as they would against a real unit.
/// </summary>
public sealed class OutboundResponder(EmulatedDeviceState state, ICryptoService crypto, int port = 7000)
{
    public async Task RunAsync(CancellationToken ct)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        ConsoleLog.Info($"Outbound UDP responder listening on 0.0.0.0:{port}");

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                ConsoleLog.Warn($"UDP receive error: {ex.Message}");
                continue;
            }

            try
            {
                await HandleDatagramAsync(udp, received, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ConsoleLog.Warn($"Failed to handle UDP datagram from {received.RemoteEndPoint}: {ex.Message}");
            }
        }
    }

    private async Task HandleDatagramAsync(UdpClient udp, UdpReceiveResult received, CancellationToken ct)
    {
        var requestText = Encoding.UTF8.GetString(received.Buffer);
        using var doc = JsonDocument.Parse(requestText);
        var type = doc.RootElement.GetProperty("t").GetString();

        switch (type)
        {
            case "scan":
                await HandleScanAsync(udp, received.RemoteEndPoint, ct).ConfigureAwait(false);
                break;

            case "pack":
                await HandlePackAsync(udp, doc.RootElement, received.RemoteEndPoint, ct).ConfigureAwait(false);
                break;

            default:
                ConsoleLog.Warn($"Outbound responder: ignoring unknown request type '{type}'");
                break;
        }
    }

    private async Task HandleScanAsync(UdpClient udp, IPEndPoint remote, CancellationToken ct)
    {
        var scanResponse = new ScanResponse
        {
            ResponseType = "dev",
            Mac = state.Mac,
            Cid = state.Mac,
            Brand = "gree",
            Model = "emulator",
            Name = state.Name,
            Version = "V1.00",
        };

        await SendPackAsync(udp, remote, JsonSerializer.Serialize(scanResponse), cryptoKey: null, ct).ConfigureAwait(false);
    }

    private async Task HandlePackAsync(UdpClient udp, JsonElement root, IPEndPoint remote, CancellationToken ct)
    {
        var innerCipher = root.GetProperty("pack").GetString() ?? string.Empty;
        var isBind = root.TryGetProperty("i", out var idElement) && idElement.GetInt32() == 1;

        if (isBind)
        {
            // Bind requests are always encrypted with the default (well-known) key.
            _ = crypto.Decrypt(innerCipher);

            var bindResponse = new BindResponse
            {
                ResponseType = "bindok",
                Mac = state.Mac,
                ResultCode = 200,
                CryptoKey = state.CryptoKey,
            };
            await SendPackAsync(udp, remote, JsonSerializer.Serialize(bindResponse), cryptoKey: null, ct).ConfigureAwait(false);
            return;
        }

        var innerJson = crypto.Decrypt(innerCipher, state.CryptoKey);
        using var innerDoc = JsonDocument.Parse(innerJson);
        var innerType = innerDoc.RootElement.GetProperty("t").GetString();

        var responseJson = innerType switch
        {
            "status" => BuildStatusResponse(innerDoc.RootElement),
            "cmd" => BuildCmdResponse(innerDoc.RootElement),
            _ => null,
        };

        if (responseJson is null)
        {
            ConsoleLog.Warn($"Outbound responder: ignoring unknown pack command type '{innerType}'");
            return;
        }

        await SendPackAsync(udp, remote, responseJson, state.CryptoKey, ct).ConfigureAwait(false);
    }

    private string BuildStatusResponse(JsonElement inner)
    {
        var cols = inner.GetProperty("cols").EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToArray();
        var dat = cols.Select(state.ValueForColumn).ToArray();

        return JsonSerializer.Serialize(new { t = "ok", mac = state.Mac, r = 200, cols, dat });
    }

    private string BuildCmdResponse(JsonElement inner)
    {
        var opt = inner.GetProperty("opt").EnumerateArray().Select(o => o.GetString() ?? string.Empty).ToArray();
        var values = inner.GetProperty("p").EnumerateArray().Select(p => p.GetString() ?? string.Empty).ToArray();

        for (var i = 0; i < opt.Length && i < values.Length; i++)
        {
            switch (opt[i])
            {
                case "name":
                    state.Name = values[i];
                    ConsoleLog.Info($"Remote 'set device name' -> '{state.Name}'");
                    break;

                case "host":
                    state.Host = values[i];
                    ConsoleLog.Info($"Remote 'set remote host' -> '{state.Host}'");
                    break;
            }
        }

        return JsonSerializer.Serialize(new { t = "res", mac = state.Mac, r = 200, opt, p = values });
    }

    private async Task SendPackAsync(UdpClient udp, IPEndPoint remote, string innerJson, string? cryptoKey, CancellationToken ct)
    {
        var encrypted = crypto.Encrypt(innerJson, cryptoKey);
        var envelope = JsonSerializer.Serialize(new { t = "pack", pack = encrypted });
        var bytes = Encoding.UTF8.GetBytes(envelope);
        await udp.SendAsync(bytes, remote, ct).ConfigureAwait(false);
    }
}
