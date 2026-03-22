using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal abstract class CommandBase
{
    /// <summary>
    /// Command type identifier for the Gree AC device command
    /// </summary>
    [JsonPropertyName("t")]
    public string Type { get; protected set; } = "cmd";
}