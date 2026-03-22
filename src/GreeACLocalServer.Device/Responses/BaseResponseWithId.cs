using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class BaseResponseWithId : BaseResponse
{
    /// <summary>
    /// MAC address (mostly)
    /// </summary>
    [JsonPropertyName("cid")]
    public string Cid { get; set; } = string.Empty;

    [JsonPropertyName("uid")]
    public int Uid { get; set; }
}