using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class ScanResponse : BaseResponseWithMac
{
    /// <summary>
    /// MAC address (mostly)
    /// </summary>
    [JsonPropertyName("cid")]
    public string Cid { get; set; } = string.Empty;

    [JsonPropertyName("bc")]
    public string Bc { get; set; } = string.Empty;

    /// <summary>
    /// Brand
    /// </summary>
    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// Catalog
    /// </summary>
    [JsonPropertyName("catalog")]
    public string Catalog { get; set; } = string.Empty;


    [JsonPropertyName("mid")]
    public string Mid { get; set; } = string.Empty;

    /// <summary>
    /// Model
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Series
    /// </summary>
    [JsonPropertyName("series")]
    public string Series { get; set; } = string.Empty;

    [JsonPropertyName("vender")]
    public string Vender { get; set; } = string.Empty;

    /// <summary>
    /// Firmware version
    /// </summary>
    [JsonPropertyName("ver")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("lock")]
    public int Lock { get; set; } = 0;
}
