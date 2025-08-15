using System.Text.Json.Serialization;

namespace GreeACLocalServer.Api.Responses;

public class BaseResponseWithId : BaseResponse
{
    [JsonPropertyName("cid")]
    public string Cid { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public int Uid { get; set; }
}