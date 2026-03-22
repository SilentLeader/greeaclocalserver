using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class QueryResponse : BaseResponseWithResultCode
{
    [JsonPropertyName("cols")]
    public List<string> ParameterNames { get; set; } = [];

    [JsonPropertyName("dat")]
    public List<string> ParameterValues { get; set; } = [];
}