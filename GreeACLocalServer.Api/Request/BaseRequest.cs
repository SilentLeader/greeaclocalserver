using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Request;

public abstract class BaseRequest
{
    [JsonPropertyName("t")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("mac")]
    public string MacAddress { get; set; } = string.Empty;
}