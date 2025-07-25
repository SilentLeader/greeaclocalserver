using System;
using GreeACHeartBeatServer.Api.Request;
using GreeACHeartBeatServer.Api.Responses;
using System.Text.Json;
using GreeACHeartBeatServer.Api.ValueObjects;
using GreeACHeartBeatServer.Api.Services;
using Microsoft.Extensions.Options;
using GreeACHeartBeatServer.Api.Options;
using Microsoft.Extensions.Logging;

namespace GreeACHeartBeatServer.Api.Services;

public class MessageHandlerService
{
    private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        WriteIndented = false
    };

    private readonly CryptoService _cryptoService;
    private readonly ServerOptions _serverOptions;
    private readonly ILogger<MessageHandlerService> _logger;

    public MessageHandlerService(CryptoService cryptoService, IOptions<ServerOptions> serverOptions, ILogger<MessageHandlerService> logger)
    {
        _cryptoService = cryptoService;
        _serverOptions = serverOptions.Value;
        _logger = logger;
    }

    public GreeHandlerResponse GetResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogWarning("Empty message");
            return HandleUnknownCommand(true);
        }

        DefaultRequest request;
        try
        {
            request = JsonSerializer.Deserialize<DefaultRequest>(input);
        }
        catch (JsonException e)
        {
            var inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            _logger.LogWarning(e, "Invalid message format. Input bytes: {InputBytes}", inputBytes);
            return HandleUnknownCommand();
        }

        _logger.LogDebug("Request: {Input}", input.Replace("\n", string.Empty));
        var response = request.Type switch
        {
            CommandType.Discover => HandleDiscover(request),
            CommandType.Pack => HandlePack(request),
            CommandType.Time => HandleTime(),
            CommandType.HeartBeat => HandleHeartbeat(),
            _ => HandleUnknownCommand(),
        };

        _logger.LogDebug("Response: {Response}", response.Data);
        response.Data = response.Data.Trim() + "\n";

        return response;
    }

    private GreeHandlerResponse HandleDiscover(DefaultRequest req)
    {
        _logger.LogInformation("Request: Discover");

        var discoverResponse = new DiscoverResponse
        {
            ServerHost = _serverOptions.DomainName,
            ServerPort = _serverOptions.Port,
            HostOrIpAddress = _serverOptions.ExternalIp,
            Ip = _serverOptions.ExternalIp,
            SecondaryIp = _serverOptions.ExternalIp,
            Protocol = "TCP",
            ResponseType = ResponseType.Server,
            TcpPort = _serverOptions.Port,
            UdpPort = _serverOptions.Port
        };

        var response = new Response
        {
            ResponseType = ResponseType.Pack,
            ObjectCount = 1,
            Cid = "",
            MacAddress = req.MacAddress,
            Uid = 0,
            Data = _cryptoService.Encrypt(JsonSerializer.Serialize(discoverResponse, _jsonSerializerOptions))
        };

        return new GreeHandlerResponse
        {
            Data = JsonSerializer.Serialize(response, _jsonSerializerOptions),
            KeepAlive = false
        };
    }

    private GreeHandlerResponse HandlePack(DefaultRequest req)
    {
        Pack pack = JsonSerializer.Deserialize<Pack>(_cryptoService.Decrypt(req.Pack));
        switch (pack.Type)
        {
            case "devLogin":
                return HandleDevLogin(pack);

            default:
                _logger.LogWarning("Request Pack unknown: {Type}", pack.Type);
                return new GreeHandlerResponse
                {
                    Data = string.Empty,
                    KeepAlive = false
                };
        }
    }

    private GreeHandlerResponse HandleDevLogin(Pack pack)
    {
        _logger.LogInformation("Request: devLogin");

        var normalizedMac = NormalizeMac(pack.MacAddress);

        var loginData = new LoginResponse
        {
            ResponseType = ResponseType.LoginResponse,
            Cid = normalizedMac,
            ResponseCode = 200,
            Uid = 0
        };

        var responseData = new Response
        {
            ResponseType = CommandType.Pack,
            ObjectCount = 1,
            MacAddress = string.Empty,
            Cid = string.Empty,
            Uid = 0,
            Data = _cryptoService.Encrypt(JsonSerializer.Serialize(loginData, _jsonSerializerOptions))
        };

        return new GreeHandlerResponse
        {
            Data = JsonSerializer.Serialize(responseData, _jsonSerializerOptions),
            KeepAlive = true
        };
    }

    private static GreeHandlerResponse HandleTime()
    {
        var responseData = new TimeResponse
        {
            ResponseType = ResponseType.Time,
            Time = DateTime.Now.ToLocalTime().ToString("yyyy-MM-ddHH:mm:ss")
        };

        return new GreeHandlerResponse
        {
            Data = JsonSerializer.Serialize(responseData, _jsonSerializerOptions),
            KeepAlive = true
        };
    }

    private static GreeHandlerResponse HandleHeartbeat()
    {
        var responseData = new HeartBeatResponse
        {
            ResponseType = ResponseType.HeartBeatOk
        };

        return new GreeHandlerResponse
        {
            Data = JsonSerializer.Serialize(responseData, _jsonSerializerOptions),
            KeepAlive = true
        };
    }

    private static GreeHandlerResponse HandleUnknownCommand(bool keepAlive = false)
    {
        return new GreeHandlerResponse
        {
            Data = string.Empty,
            KeepAlive = keepAlive
        };
    }

    private static string NormalizeMac(string obscuredMac)
    {
        char[] obscuredArr = obscuredMac.ToCharArray();
        char[] normalizedArr = new char[12];

        normalizedArr[0] = obscuredArr[8];
        normalizedArr[1] = obscuredArr[9];
        normalizedArr[2] = obscuredArr[14];
        normalizedArr[3] = obscuredArr[15];
        normalizedArr[4] = obscuredArr[2];
        normalizedArr[5] = obscuredArr[3];
        normalizedArr[6] = obscuredArr[10];
        normalizedArr[7] = obscuredArr[11];
        normalizedArr[8] = obscuredArr[4];
        normalizedArr[9] = obscuredArr[5];
        normalizedArr[10] = obscuredArr[0];
        normalizedArr[11] = obscuredArr[1];

        return new string(normalizedArr);
    }
}
