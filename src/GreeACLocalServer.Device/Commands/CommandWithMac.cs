using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal abstract class CommandWithMac : CommandBase
{
    /// <summary>
    /// MAC address
    /// </summary>
    [JsonPropertyName("mac")]
    public string Mac { get; set; } = string.Empty;
}