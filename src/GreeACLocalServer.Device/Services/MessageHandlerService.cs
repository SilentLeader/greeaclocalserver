using System.Text.Json;
using GreeACLocalServer.Device.DeviceRequests;
using GreeACLocalServer.Device.Interfaces;
using GreeACLocalServer.Device.Models;
using GreeACLocalServer.Device.Responses;
using GreeACLocalServer.Device.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace GreeACLocalServer.Device.Services;

internal class MessageHandlerService(ICryptoService cryptoService, IOptions<ServerOptions> serverOptions, ILogger<MessageHandlerService> logger) : IMessageHandlerService
{
    private static JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower,
        WriteIndented = false
    };

    private readonly ICryptoService _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    private readonly ServerOptions _deviceHandlerOptions = serverOptions?.Value ?? throw new ArgumentNullException(nameof(serverOptions));
    private readonly ILogger<MessageHandlerService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public GreeHandlerResponse GetResponse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogWarning("Empty message received");
            return HandleUnknownCommand(true);
        }

        DefaultRequest? request;
        try
        {
            _logger.LogDebug("Process device request: {input}", input);
            request = JsonSerializer.Deserialize<DefaultRequest>(input);
        }
        catch (JsonException e)
        {
            var inputPreview = input.Length > 50 ? input[..50] + "..." : input;
            _logger.LogWarning(e, "Invalid message format: {InputLength} bytes", input.Length);
            return HandleUnknownCommand();
        }

        if (request == null)
        {
            _logger.LogWarning("Deserialization returned null");
            return HandleUnknownCommand();
        }

        _logger.LogDebug("Processing request type {RequestType}", request.Type);
        var response = request.Type switch
        {
            CommandType.Discover => HandleDiscover(request),
            CommandType.Pack => HandlePack(request),
            CommandType.Time => HandleTime(),
            CommandType.HeartBeat => HandleHeartbeat(),
            _ => HandleUnknownCommand(),
        };

        _logger.LogDebug("Response generated for request type {RequestType}", request.Type);
        response.Data = response.Data.Trim() + "\n";
        response.MacAddress = !string.IsNullOrWhiteSpace(request.MacAddress) ? request.MacAddress : request.CID;
        _logger.LogDebug("Response data {data}", response.Data);

        return response;
    }

    private GreeHandlerResponse HandleDiscover(DefaultRequest req)
    {
        _logger.LogDebug("Handling Discover request");

        ArgumentException.ThrowIfNullOrWhiteSpace(_deviceHandlerOptions.DomainName);
        ArgumentException.ThrowIfNullOrWhiteSpace(_deviceHandlerOptions.ExternalIp);

        var discoverResponse = new DiscoverResponse
        {
            ServerHost = _deviceHandlerOptions.DomainName,
            ServerPort = ServerOption.PORT,
            HostOrIpAddress = _deviceHandlerOptions.ExternalIp,
            Ip = _deviceHandlerOptions.ExternalIp,
            SecondaryIp = _deviceHandlerOptions.ExternalIp,
            Protocol = "TCP",
            ResponseType = ResponseType.Server,
            TcpPort = ServerOption.PORT,
            UdpPort = ServerOption.PORT
        };

        var rawPackData = JsonSerializer.Serialize(discoverResponse, _jsonSerializerOptions);

        var response = new PackResponse
        {
            ResponseType = ResponseType.Pack,
            ObjectCount = 1,
            Cid = "",
            MacAddress = req.MacAddress,
            Uid = 0,
            Data = _cryptoService.Encrypt(rawPackData)
        };

        var rawData = JsonSerializer.Serialize(response, _jsonSerializerOptions);

        return new GreeHandlerResponse
        {
            Data = rawData,
            KeepAlive = false,
            MacAddress = req.MacAddress
        };
    }

    private GreeHandlerResponse HandlePack(DefaultRequest req)
    {
        Pack? pack = JsonSerializer.Deserialize<Pack>(_cryptoService.Decrypt(req.Pack));
        if (pack == null)
        {
            _logger.LogWarning("Pack deserialization returned null");
            return new GreeHandlerResponse
            {
                Data = string.Empty,
                KeepAlive = false,
                MacAddress = req.MacAddress
            };
        }
        switch (pack.Type)
        {
            case "devLogin":
                var loginResponse = HandleDevLogin(pack);
                loginResponse.MacAddress = req.MacAddress;
                return loginResponse;

            default:
                _logger.LogWarning("Unknown pack type: {Type}", pack.Type);
                return new GreeHandlerResponse
                {
                    Data = string.Empty,
                    KeepAlive = false,
                    MacAddress = req.MacAddress
                };
        }
    }

    private GreeHandlerResponse HandleDevLogin(Pack pack)
    {
        _logger.LogInformation("Handling devLogin for device");

        var normalizedMac = NormalizeMac(pack.MacAddress);

        var loginData = new LoginResponse
        {
            ResponseType = ResponseType.LoginResponse,
            Cid = normalizedMac,
            ResponseCode = 200,
            Uid = 0
        };

        var responseData = new PackResponse
        {
            ResponseType = ResponseType.Pack,
            ObjectCount = 1,
            MacAddress = string.Empty,
            Cid = string.Empty,
            Uid = 0,
            Data = _cryptoService.Encrypt(JsonSerializer.Serialize(loginData, _jsonSerializerOptions))
        };

        return new GreeHandlerResponse
        {
            Data = JsonSerializer.Serialize(responseData, _jsonSerializerOptions),
            KeepAlive = true,
            MacAddress = pack.MacAddress
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
            KeepAlive = true,
            MacAddress = null
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
            KeepAlive = true,
            MacAddress = null
        };
    }

    private static GreeHandlerResponse HandleUnknownCommand(bool keepAlive = false)
    {
        return new GreeHandlerResponse
        {
            Data = string.Empty,
            KeepAlive = keepAlive,
            MacAddress = null
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
