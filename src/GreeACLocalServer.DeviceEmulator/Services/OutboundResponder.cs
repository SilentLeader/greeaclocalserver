using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GreeACLocalServer.Device.Crypto;
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
/// <remarks>
/// In <see cref="GreeProtocolGeneration.V2Gcm"/> mode it behaves like a newer Wi-Fi
/// module: the scan is still answered in ECB, but bind and pack commands must be
/// AES-GCM - anything else is silently dropped, as a real unit does.
/// </remarks>
public sealed class OutboundResponder(
    EmulatedDeviceState state,
    ICryptoService crypto,
    GreeProtocolGeneration generation = GreeProtocolGeneration.V1Ecb,
    int port = 7000)
{
    private readonly IGreeCipher _scanCipher = new AesEcbGreeCipher(crypto);

    private readonly IGreeCipher _cipher = generation == GreeProtocolGeneration.V2Gcm
        ? new AesGcmGreeCipher()
        : new AesEcbGreeCipher(crypto);

    public async Task RunAsync(CancellationToken ct)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        ConsoleLog.Info($"Outbound UDP responder listening on 0.0.0.0:{port} ({generation})");

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
                var reply = BuildReply(Encoding.UTF8.GetString(received.Buffer));
                if (reply is not null)
                {
                    await udp.SendAsync(Encoding.UTF8.GetBytes(reply), received.RemoteEndPoint, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ConsoleLog.Warn($"Failed to handle UDP datagram from {received.RemoteEndPoint}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Builds the reply datagram for one request datagram, or <c>null</c> when the
    /// emulated device stays silent (unknown request, wrong protocol generation).
    /// </summary>
    public string? BuildReply(string requestText)
    {
        using var doc = JsonDocument.Parse(requestText);
        var type = doc.RootElement.GetProperty("t").GetString();

        switch (type)
        {
            case "scan":
                return BuildScanReply();

            case "pack":
                return BuildPackReply(doc.RootElement);

            default:
                ConsoleLog.Warn($"Outbound responder: ignoring unknown request type '{type}'");
                return null;
        }
    }

    private string BuildScanReply()
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

        return BuildEnvelope(_scanCipher, JsonSerializer.Serialize(scanResponse), cryptoKey: null);
    }

    private string? BuildPackReply(JsonElement root)
    {
        var innerCipher = root.GetProperty("pack").GetString() ?? string.Empty;
        var tag = root.TryGetProperty("tag", out var tagElement) ? tagElement.GetString() : null;
        var isBind = root.TryGetProperty("i", out var idElement) && idElement.GetInt32() == 1;

        // Bind requests are always encrypted with the generation's generic (well-known) key.
        var requestKey = isBind ? null : state.CryptoKey;
        if (!_cipher.TryDecrypt(innerCipher, tag, requestKey, out var innerJson))
        {
            ConsoleLog.Warn($"Outbound responder: ignoring a pack that is not valid {generation} (bind={isBind})");
            return null;
        }

        if (isBind)
        {
            var bindResponse = new BindResponse
            {
                ResponseType = "bindok",
                Mac = state.Mac,
                ResultCode = 200,
                CryptoKey = state.CryptoKey,
            };
            return BuildEnvelope(_cipher, JsonSerializer.Serialize(bindResponse), cryptoKey: null);
        }

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
            return null;
        }

        return BuildEnvelope(_cipher, responseJson, state.CryptoKey);
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

    private static string BuildEnvelope(IGreeCipher cipher, string innerJson, string? cryptoKey)
    {
        var encrypted = cipher.Encrypt(innerJson, cryptoKey);
        return encrypted.Tag is null
            ? JsonSerializer.Serialize(new { t = "pack", pack = encrypted.Pack })
            : JsonSerializer.Serialize(new { t = "pack", pack = encrypted.Pack, tag = encrypted.Tag });
    }
}
