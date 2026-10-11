using System.Text.Json;
using GreeACLocalServer.Device.Crypto;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using GreeACLocalServer.DeviceEmulator.Models;
using GreeACLocalServer.DeviceEmulator.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreeACLocalServer.DeviceEmulator.Tests;

/// <summary>
/// Drives the emulator's outbound responder through <c>scan -&gt; bind -&gt; status</c>
/// without sockets, for both protocol generations.
/// </summary>
public class OutboundResponderTests
{
    private const string Mac = "aabbccddeeff";
    private const string DeviceKey = "0123456789abcdef";

    private static readonly CryptoService Crypto = new(
        new StaticOptionsMonitor<EncryptionOptions>(new EncryptionOptions { DefaultCryptoKey = "a3K8Bx%2r8Y7#xDh" }),
        NullLogger<CryptoService>.Instance);

    private static OutboundResponder CreateResponder(GreeProtocolGeneration generation)
        => new(new EmulatedDeviceState { Mac = Mac, CryptoKey = DeviceKey, Name = "Living Room" }, Crypto, generation);

    private static string PackRequest(IGreeCipher cipher, string innerJson, string? key, int id)
    {
        var encrypted = cipher.Encrypt(innerJson, key);
        return encrypted.Tag is null
            ? JsonSerializer.Serialize(new { t = "pack", i = id, tcid = Mac, pack = encrypted.Pack })
            : JsonSerializer.Serialize(new { t = "pack", i = id, tcid = Mac, pack = encrypted.Pack, tag = encrypted.Tag });
    }

    private static JsonElement Open(IGreeCipher cipher, string? envelopeJson, string? key)
    {
        Assert.NotNull(envelopeJson);
        using var envelope = JsonDocument.Parse(envelopeJson);
        var tag = envelope.RootElement.TryGetProperty("tag", out var t) ? t.GetString() : null;
        Assert.True(cipher.TryDecrypt(envelope.RootElement.GetProperty("pack").GetString()!, tag, key, out var inner));
        return JsonDocument.Parse(inner).RootElement.Clone();
    }

    [Theory]
    [InlineData(GreeProtocolGeneration.V1Ecb)]
    [InlineData(GreeProtocolGeneration.V2Gcm)]
    public void ScanBindStatus_Conversation_Works(GreeProtocolGeneration generation)
    {
        var responder = CreateResponder(generation);
        IGreeCipher ecb = new AesEcbGreeCipher(Crypto);
        IGreeCipher cipher = generation == GreeProtocolGeneration.V2Gcm ? new AesGcmGreeCipher() : ecb;

        // The scan is answered in ECB by both generations.
        var scan = Open(ecb, responder.BuildReply("{\"t\":\"scan\"}"), null);
        Assert.Equal(Mac, scan.GetProperty("mac").GetString());

        var bind = Open(cipher, responder.BuildReply(PackRequest(cipher, $"{{\"mac\":\"{Mac}\",\"t\":\"bind\",\"uid\":0}}", null, 1)), null);
        Assert.Equal("bindok", bind.GetProperty("t").GetString());
        Assert.Equal(DeviceKey, bind.GetProperty("key").GetString());

        var status = Open(cipher, responder.BuildReply(PackRequest(cipher, $"{{\"cols\":[\"name\"],\"mac\":\"{Mac}\",\"t\":\"status\"}}", DeviceKey, 0)), DeviceKey);
        Assert.Equal("Living Room", status.GetProperty("dat")[0].GetString());
    }

    [Fact]
    public void GcmDevice_IgnoresEcbBind()
    {
        var responder = CreateResponder(GreeProtocolGeneration.V2Gcm);
        var ecbBind = PackRequest(new AesEcbGreeCipher(Crypto), $"{{\"mac\":\"{Mac}\",\"t\":\"bind\",\"uid\":0}}", null, 1);

        Assert.Null(responder.BuildReply(ecbBind));
    }

    [Fact]
    public void GcmDevice_TagsItsReplies()
    {
        var responder = CreateResponder(GreeProtocolGeneration.V2Gcm);
        var bind = PackRequest(new AesGcmGreeCipher(), $"{{\"mac\":\"{Mac}\",\"t\":\"bind\",\"uid\":0}}", null, 1);

        using var envelope = JsonDocument.Parse(responder.BuildReply(bind)!);
        Assert.True(envelope.RootElement.TryGetProperty("tag", out _));
    }

    [Fact]
    public void EcbDevice_DoesNotTagItsReplies()
    {
        var responder = CreateResponder(GreeProtocolGeneration.V1Ecb);
        var bind = PackRequest(new AesEcbGreeCipher(Crypto), $"{{\"mac\":\"{Mac}\",\"t\":\"bind\",\"uid\":0}}", null, 1);

        using var envelope = JsonDocument.Parse(responder.BuildReply(bind)!);
        Assert.False(envelope.RootElement.TryGetProperty("tag", out _));
    }
}
