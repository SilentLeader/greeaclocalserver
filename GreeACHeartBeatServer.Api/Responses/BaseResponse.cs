using System.Text.Json.Serialization;

namespace GreeACHeartBeatServer.Api.Responses
{
    public abstract class BaseResponse
    {
        [JsonPropertyName("t")]
        public string ResponseType { get; set; }

        
    }
}