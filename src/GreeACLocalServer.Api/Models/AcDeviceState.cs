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

        /// <summary>
        /// Distinct connection endpoints the device has been observed using, ordered
        /// by port. Never expires; entries are only dropped when the device is removed.
        /// </summary>
        public IReadOnlyList<DeviceEndpoint> Endpoints { get; init; } = [];
    }

    /// <summary>A single observed (port, TLS) connection endpoint with its last-seen time.</summary>
    public record DeviceEndpoint(int Port, bool IsTls, DateTime LastSeenUtc);
}
