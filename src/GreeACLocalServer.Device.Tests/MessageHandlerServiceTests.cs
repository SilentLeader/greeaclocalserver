using System.Text.Json;
using System.Text.RegularExpressions;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

public class MessageHandlerServiceTests
{
    private const string DefaultKey = "a3K8Bx%2r8Y7#xDh"; // 16 chars, matches CryptoServiceTests

    private static CryptoService CreateCrypto()
    {
        var monitor = new Mock<IOptionsMonitor<EncryptionOptions>>();
        monitor.Setup(x => x.CurrentValue).Returns(new EncryptionOptions { DefaultCryptoKey = DefaultKey });
        return new CryptoService(monitor.Object, NullLogger<CryptoService>.Instance);
    }

    private static MessageHandlerService CreateService(
        CryptoService? crypto = null,
        ServerOptions? options = null)
    {
        crypto ??= CreateCrypto();
        options ??= new ServerOptions { DomainName = "gree.example.com", ExternalIp = "203.0.113.7" };
        return new MessageHandlerService(
            crypto,
            Options.Create(options),
            NullLogger<MessageHandlerService>.Instance);
    }

    /// <summary>Mirror of the internal NormalizeMac transform for assertions.</summary>
    private static string NormalizeMac(string m) => new string(new[]
    {
        m[8], m[9], m[14], m[15], m[2], m[3], m[10], m[11], m[4], m[5], m[0], m[1]
    });

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetResponse_EmptyInput_ReturnsUnknownWithoutThrowing(string input)
    {
        var response = CreateService().GetResponse(input);

        Assert.Equal(string.Empty, response.Data);
    }

    [Fact]
    public void GetResponse_InvalidJson_ReturnsUnknownWithoutThrowing()
    {
        var response = CreateService().GetResponse("{ this is not json");

        Assert.Equal(string.Empty, response.Data);
    }

    [Fact]
    public void GetResponse_UnknownType_ReturnsEmptyDataAndNoKeepAlive()
    {
        var response = CreateService().GetResponse("{\"t\":\"totally-unknown\"}");

        Assert.Equal(string.Empty, response.Data);
        Assert.False(response.KeepAlive);
    }

    [Fact]
    public void GetResponse_Heartbeat_ReturnsHbOkAndKeepsAlive()
    {
        var response = CreateService().GetResponse("{\"t\":\"hb\"}");

        using var doc = JsonDocument.Parse(response.Data);
        Assert.Equal("hbok", doc.RootElement.GetProperty("t").GetString());
        Assert.True(response.KeepAlive);
    }

    [Fact]
    public void GetResponse_PackWithEmptyPackField_DoesNotThrowAndReturnsEmptyData()
    {
        var response = CreateService().GetResponse("{\"t\":\"pack\",\"pack\":\"\",\"mac\":\"AABBCC\"}");

        Assert.Equal(string.Empty, response.Data);
    }

    [Fact]
    public void GetResponse_PackWithGarbagePack_DoesNotThrow()
    {
        var response = CreateService().GetResponse("{\"t\":\"pack\",\"pack\":\"not-base64!!\",\"mac\":\"AABBCC\"}");

        Assert.Equal(string.Empty, response.Data);
    }

    [Fact]
    public void GetResponse_DevLogin_ReturnsLoginResponseWithNormalizedCid()
    {
        var crypto = CreateCrypto();
        const string mac = "0123456789abcdef";
        var innerPack = crypto.Encrypt($"{{\"t\":\"devLogin\",\"mac\":\"{mac}\"}}");
        var request = $"{{\"t\":\"pack\",\"pack\":\"{innerPack}\",\"mac\":\"{mac}\"}}";

        var response = CreateService(crypto).GetResponse(request);

        Assert.True(response.KeepAlive);

        using var outer = JsonDocument.Parse(response.Data);
        var decrypted = crypto.Decrypt(outer.RootElement.GetProperty("pack").GetString()!);
        using var login = JsonDocument.Parse(decrypted);
        Assert.Equal("loginRes", login.RootElement.GetProperty("t").GetString());
        Assert.Equal(NormalizeMac(mac), login.RootElement.GetProperty("cid").GetString());
    }

    [Fact]
    public void GetResponse_DevLogin_ShortMac_DoesNotThrow()
    {
        var crypto = CreateCrypto();
        var innerPack = crypto.Encrypt("{\"t\":\"devLogin\",\"mac\":\"abc\"}");
        var request = $"{{\"t\":\"pack\",\"pack\":\"{innerPack}\",\"mac\":\"abc\"}}";

        var exception = Record.Exception(() => CreateService(crypto).GetResponse(request));

        Assert.Null(exception);
    }

    [Fact]
    public void GetResponse_Time_MatchesFinalizedFormat()
    {
        var response = CreateService().GetResponse("{\"t\":\"tm\"}");

        using var doc = JsonDocument.Parse(response.Data);
        var time = doc.RootElement.GetProperty("time").GetString();
        // Format left unchanged by WP-03/F10 (date and time not separated).
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}\d{2}:\d{2}:\d{2}$"), time!);
    }

    [Fact]
    public void GetResponse_Discover_Configured_ReturnsPackContainingDomain()
    {
        var crypto = CreateCrypto();
        var service = CreateService(crypto, new ServerOptions
        {
            DomainName = "gree.example.com",
            ExternalIp = "203.0.113.7"
        });

        var response = service.GetResponse("{\"t\":\"dis\",\"mac\":\"AABBCC\"}");

        using var doc = JsonDocument.Parse(response.Data);
        var decrypted = crypto.Decrypt(doc.RootElement.GetProperty("pack").GetString()!);
        Assert.Contains("gree.example.com", decrypted);
    }

    [Fact]
    public void GetResponse_Discover_MissingDomain_DoesNotPropagateException()
    {
        var service = CreateService(options: new ServerOptions { DomainName = null, ExternalIp = null });

        var exception = Record.Exception(() => service.GetResponse("{\"t\":\"dis\",\"mac\":\"AABBCC\"}"));

        Assert.Null(exception);
    }
}
