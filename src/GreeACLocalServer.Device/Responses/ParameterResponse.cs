using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class ParameterResponse : BaseResponseWithResultCode
{
    [JsonPropertyName("opt")]
    public List<string> ParameterNames { get; set; } = [];


    [JsonPropertyName("p")]
    public List<object> ParameterValues { get; set; } = [];
}