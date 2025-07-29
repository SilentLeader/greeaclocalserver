using System.Text.Json.Serialization;

namespace GreeACHeartBeatServer.Api.Responses
{
    public class TimeResponse : BaseResponse
    {   
        [JsonPropertyName("time")]
        public string Time { get; set; }
    }
}