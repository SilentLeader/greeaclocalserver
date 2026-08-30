namespace GreeACLocalServer.Shared.Contracts;

public record DeviceDto(
    string MacAddress,
    string IpAddress,
    string DNSName,
    DateTime LastConnectionTimeUtc,
    IReadOnlyList<DeviceEndpointDto>? Endpoints = null)
{
    /// <summary>Connection endpoints the device has been seen using, ordered by port.</summary>
    public IReadOnlyList<DeviceEndpointDto> Endpoints { get; init; } = Endpoints ?? [];
}
