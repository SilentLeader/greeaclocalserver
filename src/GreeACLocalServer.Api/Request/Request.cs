using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Request;

public class DefaultRequest : BaseRequest
{
    [JsonPropertyName("pack")]
    public string Pack { get; set; } = string.Empty;
}