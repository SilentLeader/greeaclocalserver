using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class BindCommand : CommandWithMac
{
    [JsonPropertyName("uid")]
    public int UId { get; set; } = 0;

    /// <summary>
    /// Device MAC again; only sent on the V2 (GCM) generation.
    /// </summary>
    [JsonPropertyName("cid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cid { get; set; }

    public BindCommand()
    {
        Type = "bind";
    }
}