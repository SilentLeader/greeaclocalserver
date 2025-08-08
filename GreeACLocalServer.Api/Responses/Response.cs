using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Responses;

public class Response : BaseResponseWithId
{
    [JsonPropertyName("i")]
    public int ObjectCount { get; set; }
    
    [JsonPropertyName("tcid")]
    public string MacAddress { get; set; } = string.Empty;
    
    [JsonPropertyName("pack")]
    public string Data { get; set; } = string.Empty;
}