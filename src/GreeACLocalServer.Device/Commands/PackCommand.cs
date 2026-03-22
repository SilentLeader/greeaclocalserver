using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class PackCommand : CommandBase
{
    [JsonPropertyName("cid")]
    public string Cid { get; } = "app";

    [JsonPropertyName("i")]
    public int Id { get; } = 0;

    [JsonPropertyName("uid")]
    public int UId { get; } = 22130;

    [JsonPropertyName("pack")]
    public string EncryptedCommand { get; }

    [JsonPropertyName("tcid")]
    public string MacAddress { get; }

    public PackCommand(
        string encryptedCommand,
        string macAddress,
        int? id = null)
    {
        EncryptedCommand = encryptedCommand;
        MacAddress = macAddress;
        Type = "pack";
        if (id != null)
        {
            Id = id.Value;
        }
    }
}