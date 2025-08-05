using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Api.Services;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Responses;
using GreeACLocalServer.Api.Request;

public class MessageHandlerServiceTests
{
    private MessageHandlerService CreateService(
        CryptoService? cryptoService = null,
        ServerOptions? serverOptions = null,
        ILogger<MessageHandlerService>? logger = null)
    {
        serverOptions ??= new ServerOptions { DomainName = "test", ExternalIp = "127.0.0.1", Port = 1234, CryptoKey = "1234567890123456" };
        var options = Mock.Of<IOptions<ServerOptions>>(o => o.Value == serverOptions);
        cryptoService ??= new CryptoService(options);
        logger ??= Mock.Of<ILogger<MessageHandlerService>>();
        return new MessageHandlerService(cryptoService, options, logger);
    }

    [Fact]
    public void GetResponse_ReturnsUnknownCommand_OnEmptyInput()
    {
        var service = CreateService();
        var result = service.GetResponse("");
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Data);
        Assert.True(result.KeepAlive);
    }

    [Fact]
    public void GetResponse_ReturnsUnknownCommand_OnInvalidJson()
    {
        var service = CreateService();
        var result = service.GetResponse("not a json");
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Data);
        Assert.False(result.KeepAlive);
    }

    [Fact]
    public void GetResponse_DiscoverRequest_ReturnsDiscoverResponse()
    {
        var service = CreateService();
        var request = new GreeACLocalServer.Api.Request.DefaultRequest
        {
            Type = GreeACLocalServer.Api.ValueObjects.CommandType.Discover,
            MacAddress = "AABBCCDDEEFF"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var result = service.GetResponse(json);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Data));
        Assert.False(result.KeepAlive);

        // Decrypt the inner response and check for ResponseType.Server (property 't')
        var cryptoService = service.GetType().GetField("_cryptoService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(service) as CryptoService;
        Assert.NotNull(cryptoService);
        using var doc = System.Text.Json.JsonDocument.Parse(result.Data);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("pack", out var packProp));
        var decrypted = cryptoService!.Decrypt(packProp.GetString()!);
        using var innerDoc = System.Text.Json.JsonDocument.Parse(decrypted);
        var innerRoot = innerDoc.RootElement;
        Assert.True(innerRoot.TryGetProperty("t", out var responseTypeProp));
        Assert.Equal("svr", responseTypeProp.GetString()); // Should match ResponseType.Server serialized as 'svr'
    }

    [Fact]
    public void GetResponse_PackRequest_UnknownPackType_ReturnsEmptyData()
    {
        var service = CreateService();
        var pack = new GreeACLocalServer.Api.Request.Pack { Type = "unknown", MacAddress = "AABBCCDDEEFF" };
        var cryptoService = service.GetType().GetField("_cryptoService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(service) as CryptoService;
        Assert.NotNull(cryptoService);
        var encryptedPack = cryptoService!.Encrypt(System.Text.Json.JsonSerializer.Serialize(pack));
        var request = new GreeACLocalServer.Api.Request.DefaultRequest
        {
            Type = GreeACLocalServer.Api.ValueObjects.CommandType.Pack,
            MacAddress = "AABBCCDDEEFF",
            Pack = encryptedPack
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var result = service.GetResponse(json);
        Assert.NotNull(result);
        Assert.Equal("\n", result.Data); // Implementation always appends a newline
        Assert.False(result.KeepAlive);
    }

    [Fact]
    public void GetResponse_TimeRequest_ReturnsTimeResponse()
    {
        var service = CreateService();
        var request = new GreeACLocalServer.Api.Request.DefaultRequest
        {
            Type = GreeACLocalServer.Api.ValueObjects.CommandType.Time,
            MacAddress = "AABBCCDDEEFF"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var result = service.GetResponse(json);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Data));
        Assert.True(result.KeepAlive);
        Assert.Contains("tm", result.Data); // Should contain ResponseType.Time
    }

    [Fact]
    public void GetResponse_HeartBeatRequest_ReturnsHeartBeatResponse()
    {
        var service = CreateService();
        var request = new GreeACLocalServer.Api.Request.DefaultRequest
        {
            Type = GreeACLocalServer.Api.ValueObjects.CommandType.HeartBeat,
            MacAddress = "AABBCCDDEEFF"
        };
        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var result = service.GetResponse(json);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Data));
        Assert.True(result.KeepAlive);
        Assert.Contains("hbok", result.Data); // Should contain ResponseType.HeartBeatOk
    }

    // Add more tests for valid requests, mocks for CryptoService, etc.
}
