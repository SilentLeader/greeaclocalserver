using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Responses;

public class QueryResponse : BaseResponseWithResultCode
{
    [JsonPropertyName("cols")]
    public List<string> ParameterNames { get; set; } = [];

    /// <summary>
    /// Raw <c>dat</c> values. GREE returns these untyped — a JSON string for
    /// text columns (<c>hid</c>, <c>name</c>, <c>host</c>) and a JSON number for
    /// the operating-state columns (<c>Pow</c>, <c>Mod</c>, <c>SetTem</c>, …), so
    /// they are kept as <see cref="JsonElement"/> and read via <see cref="ValueAsText"/>.
    /// </summary>
    [JsonPropertyName("dat")]
    public List<JsonElement> ParameterValues { get; set; } = [];

    /// <summary>
    /// The column value at <paramref name="index"/> as text: the unquoted content
    /// for a JSON string, the literal for any other kind. Returns <c>null</c> when
    /// the index is out of range.
    /// </summary>
    public string? ValueAsText(int index)
    {
        if (index < 0 || index >= ParameterValues.Count)
        {
            return null;
        }

        var element = ParameterValues[index];
        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
    }
}
