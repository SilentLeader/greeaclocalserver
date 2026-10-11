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

    /// <summary>
    /// Base64 AES-GCM authentication tag; only sent by the V2 (GCM) generation.
    /// </summary>
    [JsonPropertyName("tag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag { get; }

    [JsonPropertyName("pack")]
    public string EncryptedCommand { get; }

    [JsonPropertyName("tcid")]
    public string MacAddress { get; }

    public PackCommand(
        string encryptedCommand,
        string macAddress,
        int? id = null,
        string? tag = null)
    {
        EncryptedCommand = encryptedCommand;
        MacAddress = macAddress;
        Type = "pack";
        if (id != null)
        {
            Id = id.Value;
        }

        if (tag != null)
        {
            Tag = tag;
            // GCM units cannot be tested here, so mirror the envelope the known
            // working clients send them (uid 0) instead of our V1 value.
            UId = 0;
        }
    }
}