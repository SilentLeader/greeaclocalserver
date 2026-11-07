using System.Text.Json;
using GreeACLocalServer.Device.Commands;
using GreeACLocalServer.Device.Crypto;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Covers the V2 (AES-GCM) generation of the local UDP :7000 protocol. The known-answer
/// vectors were produced with an independent AES-GCM implementation from the protocol
/// constants (fixed nonce + AAD), so they prove wire compatibility, not just a round trip.
/// </summary>
public class AesGcmGreeCipherTests
{
    private const string DeviceKey = "0123456789abcdef";

    private readonly AesGcmGreeCipher _cipher = new();

    [Theory]
    [InlineData(null, "{\"mac\":\"aabbccddeeff\",\"t\":\"bind\",\"uid\":0}",
        "JtoT1XUt89Ky557VOSoRU8ok7oUAzgWyIl52X7OFwurdNHgGiUNkepU=", "7OLw15QmZQQLFkRgI9whIA==")]
    [InlineData(DeviceKey, "{\"cols\":[\"Pow\"],\"mac\":\"aabbccddeeff\",\"t\":\"status\"}",
        "i3fdjqdVNLBKR5/TI5nArs/+X8pKtggj7A2IdMEe40h6d3mtaRC1YsKP9m5Y9fLdA9Q=", "OeZqzZkOGYAjSDah+tM5fQ==")]
    public void Encrypt_KnownVector_MatchesTheProtocol(string? key, string plaintext, string expectedPack, string expectedTag)
    {
        var encrypted = _cipher.Encrypt(plaintext, key);

        Assert.Equal(expectedPack, encrypted.Pack);
        Assert.Equal(expectedTag, encrypted.Tag);

        Assert.True(_cipher.TryDecrypt(expectedPack, expectedTag, key, out var decrypted));
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_Decrypt_Roundtrip_KeepsUtf8Intact()
    {
        const string plaintext = "{\"name\":\"Nappali légkondi – Előszoba\"}";

        var encrypted = _cipher.Encrypt(plaintext, DeviceKey);

        Assert.True(_cipher.TryDecrypt(encrypted.Pack, encrypted.Tag, DeviceKey, out var decrypted));
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TryDecrypt_TrailingBytesAfterTheJsonObject_AreDropped()
    {
        var encrypted = _cipher.Encrypt("{\"t\":\"bindok\"}\0\0\u000f", DeviceKey);

        Assert.True(_cipher.TryDecrypt(encrypted.Pack, encrypted.Tag, DeviceKey, out var decrypted));
        Assert.Equal("{\"t\":\"bindok\"}", decrypted);
    }

    [Fact]
    public void TryDecrypt_WrongKey_ReturnsFalse()
    {
        var encrypted = _cipher.Encrypt("{\"t\":\"status\"}", DeviceKey);

        Assert.False(_cipher.TryDecrypt(encrypted.Pack, encrypted.Tag, "fedcba9876543210", out _));
        // The generic key is used when no device key is given.
        Assert.False(_cipher.TryDecrypt(encrypted.Pack, encrypted.Tag, null, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")]
    [InlineData("not base64 !!")]
    [InlineData("AAAA")]
    public void TryDecrypt_MissingOrInvalidTag_ReturnsFalse(string? tag)
    {
        var encrypted = _cipher.Encrypt("{\"t\":\"status\"}", DeviceKey);

        Assert.False(_cipher.TryDecrypt(encrypted.Pack, tag, DeviceKey, out _));
    }

    [Fact]
    public void TryDecrypt_MalformedPackOrUnusableKey_ReturnsFalse()
    {
        var encrypted = _cipher.Encrypt("{\"t\":\"status\"}", DeviceKey);

        Assert.False(_cipher.TryDecrypt("not base64 !!", encrypted.Tag, DeviceKey, out _));
        Assert.False(_cipher.TryDecrypt(encrypted.Pack, encrypted.Tag, "too-short", out _));
    }

    [Fact]
    public void PackCommand_WithoutTag_KeepsTheV1Envelope()
    {
        var json = JsonSerializer.Serialize(new PackCommand("cGFjaw==", "aabbccddeeff", 1));

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("tag", out _));
        Assert.Equal(22130, doc.RootElement.GetProperty("uid").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("i").GetInt32());
    }

    [Fact]
    public void PackCommand_WithTag_SendsItNextToThePack()
    {
        var json = JsonSerializer.Serialize(new PackCommand("cGFjaw==", "aabbccddeeff", 0, "dGFn"));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("dGFn", doc.RootElement.GetProperty("tag").GetString());
        Assert.Equal("cGFjaw==", doc.RootElement.GetProperty("pack").GetString());
        Assert.Equal("aabbccddeeff", doc.RootElement.GetProperty("tcid").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("uid").GetInt32());
        Assert.Equal("pack", doc.RootElement.GetProperty("t").GetString());
    }

    [Fact]
    public void BindCommand_OnlyCarriesCidWhenSet()
    {
        var v1 = JsonSerializer.Serialize(new BindCommand { Mac = "aabbccddeeff" });
        var v2 = JsonSerializer.Serialize(new BindCommand { Mac = "aabbccddeeff", Cid = "aabbccddeeff" });

        Assert.DoesNotContain("\"cid\"", v1);
        using var doc = JsonDocument.Parse(v2);
        Assert.Equal("aabbccddeeff", doc.RootElement.GetProperty("cid").GetString());
        Assert.Equal("aabbccddeeff", doc.RootElement.GetProperty("mac").GetString());
        Assert.Equal("bind", doc.RootElement.GetProperty("t").GetString());
    }

    [Fact]
    public void QueryStatusCommand_DefaultsToTheV1MacPlaceholder()
    {
        var json = JsonSerializer.Serialize(new QueryStatusCommand(["Pow"]));

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("mac").GetInt32());
    }
}
