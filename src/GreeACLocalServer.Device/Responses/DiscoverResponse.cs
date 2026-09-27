using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class DiscoverResponse : BaseResponse
{
    /// <summary>
    /// Telemetry ("data") upload target. Newer firmware uploads binary "fg" frames
    /// here; the GREE cloud leaves it empty. Not used for routing.
    /// </summary>
    [JsonPropertyName("datHost")]
    public string DataHost { get; set; } = string.Empty;

    [JsonPropertyName("datHostPort")]
    public int DataHostPort { get; set; }

    [JsonPropertyName("host")]
    public string HostOrIpAddress { get; set; } = string.Empty;

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("ip2")]
    public string SecondaryIp { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("tcpPort")]
    public int TcpPort { get; set; }

    [JsonPropertyName("udpPort")]
    public int UdpPort { get; set; }
}