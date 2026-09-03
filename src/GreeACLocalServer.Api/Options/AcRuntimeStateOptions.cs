namespace GreeACLocalServer.Api.Options;

/// <summary>
/// Controls the background poll that reads each AC's operating state (power,
/// mode, setpoint, unit) over outbound UDP. Bound from
/// <c>GreeServer:RuntimeStatePolling</c>. Only active in UI hosting mode.
/// </summary>
public class AcRuntimeStateOptions
{
    /// <summary>When false, no runtime-state poll runs and no outbound query traffic is emitted.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between poll cycles. Clamped to a minimum of 1.</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Only devices whose last connection was within this many minutes are polled.
    /// Independent of the <c>DeviceManager:DeviceTimeoutMinutes</c> "online" display window.
    /// </summary>
    public int OnlineWindowMinutes { get; set; } = 5;

    /// <summary>How many devices are polled concurrently within a cycle.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>After a failed poll, wait at least this long before retrying that device.</summary>
    public int FailureBackoffSeconds { get; set; } = 300;

    /// <summary>
    /// After this many consecutive failures, stop polling the device until it
    /// reconnects. 0 disables the "give up" behaviour (only the backoff applies).
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;
}
