using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Responses;

public class DiscoverResponse : BaseResponse
{
    [JsonPropertyName("datHost")]
    public string ServerHost { get; set; } = string.Empty;
    
    [JsonPropertyName("datHostPort")]
    public int ServerPort { get; set; }
    
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