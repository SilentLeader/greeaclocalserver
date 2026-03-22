using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class BaseResponseWithResultCode : BaseResponseWithMac
{
    [JsonPropertyName("r")]
    public int ResultCode { get; set; }
}