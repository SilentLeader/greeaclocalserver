
using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class QueryStatusCommand : CommandBase
{
    [JsonPropertyName("mac")]
    public int Mac { get; set; } = 0;

    [JsonPropertyName("cols")]
    public IReadOnlyCollection<string> ParameterNames { get; }

    public QueryStatusCommand(IReadOnlyCollection<string> parameterNames)
    {
        ParameterNames = parameterNames;
        Type = "status";
    }
}