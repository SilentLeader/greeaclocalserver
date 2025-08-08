using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Responses
{
    public abstract class BaseResponse
    {
        [JsonPropertyName("t")]
        public string ResponseType { get; set; } = string.Empty;
    }
}