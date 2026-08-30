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

        /// <summary>Raw <c>hid</c> firmware identifier last reported by the device.</summary>
        public string? FirmwareHid { get; init; }

        /// <summary>Parsed firmware version (e.g. <c>3.76</c>), when the <c>hid</c> was parseable.</summary>
        public string? FirmwareVersion { get; init; }

        /// <summary>Parsed firmware code used for update lookups.</summary>
        public string? FirmwareCode { get; init; }

        /// <summary>When the firmware identifier was last successfully queried from the device.</summary>
        public DateTime? FirmwareCheckedUtc { get; init; }

        /// <summary>
        /// When a firmware query was last <em>attempted</em> against the device
        /// (success or failure). Used to throttle the opportunistic background
        /// refresh so an unreachable device is not re-probed on every reconnect.
        /// </summary>
        public DateTime? FirmwareRefreshAttemptedUtc { get; init; }
    }

    /// <summary>A single observed (port, TLS) connection endpoint with its last-seen time.</summary>
    public record DeviceEndpoint(int Port, bool IsTls, DateTime LastSeenUtc);
}
