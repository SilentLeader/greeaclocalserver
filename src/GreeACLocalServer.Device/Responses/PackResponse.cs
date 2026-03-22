using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class PackResponse : BaseResponseWithId
{
    [JsonPropertyName("i")]
    public int ObjectCount { get; set; }

    [JsonPropertyName("tcid")]
    public string MacAddress { get; set; } = string.Empty;

    [JsonPropertyName("pack")]
    public string Data { get; set; } = string.Empty;
}