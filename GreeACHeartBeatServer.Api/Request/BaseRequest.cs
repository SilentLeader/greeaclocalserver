using System.Text.Json.Serialization;

namespace GreeACHeartBeatServer.Api.Request;

public abstract class BaseRequest
{
    [JsonPropertyName("t")]
    public string Type { get; set; }

    [JsonPropertyName("mac")]
    public string MacAddress { get; set; }
}