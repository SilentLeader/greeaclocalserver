using GreeACLocalServer.DeviceEmulator.Services;

namespace GreeACLocalServer.DeviceEmulator.Models;

/// <summary>Target server + connection behavior for <see cref="InboundClient"/>.</summary>
public sealed record InboundClientOptions
{
    public required string Host { get; init; }

    public required int Port { get; init; }

    public bool UseTls { get; init; }

    public bool AllowLegacyTls { get; init; }

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(60);
}
