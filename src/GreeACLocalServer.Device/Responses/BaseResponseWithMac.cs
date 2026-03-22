using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class BaseResponseWithMac : BaseResponse
{
    /// <summary>
    /// MAC address
    /// </summary>
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = string.Empty;
}