using System.Text.Json.Serialization;

namespace GreeACHeartBeatServer.Api.Responses;

public class DiscoverResponse : BaseResponse
{
    [JsonPropertyName("datHost")]
    public string ServerHost { get; set; }
    
    [JsonPropertyName("datHostPort")]
    public int ServerPort { get; set; }
    
    [JsonPropertyName("host")]
    public string HostOrIpAddress { get; set; }
    
    [JsonPropertyName("ip")]
    public string Ip { get; set; }
    
    [JsonPropertyName("ip2")]
    public string SecondaryIp { get; set; }
    
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }
    
    [JsonPropertyName("tcpPort")]
    public int TcpPort { get; set; }
    
    [JsonPropertyName("udpPort")]
    public int UdpPort { get; set; }
}