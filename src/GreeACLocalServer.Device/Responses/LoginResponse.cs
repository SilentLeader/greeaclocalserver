using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class LoginResponse : BaseResponseWithId
{
    [JsonPropertyName("r")]
    public int ResponseCode { get; set; }
}