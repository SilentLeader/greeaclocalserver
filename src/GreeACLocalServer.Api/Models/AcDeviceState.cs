namespace GreeACLocalServer.Api.Models
{
    /// <summary>
    /// Immutable snapshot of a device's last known connection state.
    /// Updates produce a new instance (see <c>with</c>) instead of mutating a shared reference.
    /// </summary>
    public record AcDeviceState
    {
        public string MacAddress { get; init; } = string.Empty;
        public string IpAddress { get; init; } = string.Empty;
        public string DNSName { get; init; } = string.Empty;
        public DateTime LastConnectionTime { get; init; }
    }
}
