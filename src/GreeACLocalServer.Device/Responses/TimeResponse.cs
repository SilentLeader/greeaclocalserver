using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class TimeResponse : BaseResponse
{
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}