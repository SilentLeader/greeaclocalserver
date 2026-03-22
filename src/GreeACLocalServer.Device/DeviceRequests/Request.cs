using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.DeviceRequests;

public class DefaultRequest : BaseRequest
{
    [JsonPropertyName("pack")]
    public string Pack { get; set; } = string.Empty;
}