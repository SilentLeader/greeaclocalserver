using System.Text.Json.Serialization;

namespace GreeACLocalServer.Device.Commands;

internal class ParameterCommand : CommandBase
{
    [JsonPropertyName("opt")]
    public IReadOnlyCollection<string> ParameterNames { get; }


    [JsonPropertyName("p")]
    public IReadOnlyCollection<object> ParameterValues { get; }

    public ParameterCommand(
        IReadOnlyCollection<string> parameterNames,
        IReadOnlyCollection<string> parameterValues)
    {
        ParameterNames = parameterNames;
        ParameterValues = parameterValues;
        Type = "cmd";
    }
}