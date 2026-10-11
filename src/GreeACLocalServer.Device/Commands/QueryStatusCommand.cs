
using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class QueryStatusCommand : CommandBase
{
    /// <summary>
    /// <c>0</c> on the V1 (ECB) generation, the device MAC on the V2 (GCM) one.
    /// </summary>
    [JsonPropertyName("mac")]
    public object Mac { get; set; } = 0;

    [JsonPropertyName("cols")]
    public IReadOnlyCollection<string> ParameterNames { get; }

    public QueryStatusCommand(IReadOnlyCollection<string> parameterNames)
    {
        ParameterNames = parameterNames;
        Type = "status";
    }
}