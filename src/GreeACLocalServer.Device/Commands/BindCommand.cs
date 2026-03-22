using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class BindCommand : CommandWithMac
{
    [JsonPropertyName("uid")]
    public int UId { get; set; } = 0;

    public BindCommand()
    {
        Type = "bind";
    }
}