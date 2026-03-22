using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class BindResponse : BaseResponseWithResultCode
{
    [JsonPropertyName("key")]
    public string? CryptoKey { get; set; }
}