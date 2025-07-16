using System.Text.Json.Serialization;

namespace GreeACHeartBeatServer.Api.Request;

public class DefaultRequest : BaseRequest
{
    [JsonPropertyName("pack")]
    public string Pack { get; set; }
}