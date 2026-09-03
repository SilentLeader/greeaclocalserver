namespace GreeACLocalServer.Api.Services;

/// <summary>
/// Periodically polls the operating state (power / mode / setpoint / unit) of
/// every AC that connected recently, so the UI can show a live-ish status.
/// Registered only in UI hosting mode — headless deployments emit no such traffic.
/// </summary>
public class AcRuntimeStatePollingService(
    IInternalDeviceManagerService deviceManager,
    IOptionsMonitor<AcRuntimeStateOptions> options,
    ILogger<AcRuntimeStatePollingService> logger) : BackgroundService
{
    private readonly IInternalDeviceManagerService _deviceManager = deviceManager;
    private readonly IOptionsMonitor<AcRuntimeStateOptions> _options = options;
    private readonly ILogger<AcRuntimeStatePollingService> _logger = logger;

    private readonly TaskCompletionSource _firstCycleCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// MACs we have already logged as "polling stopped". Cleared for a device once
    /// it becomes a poll target again (reconnect resets its failure counter), so the
    /// give-up notice is logged once per outage rather than every cycle.
    /// </summary>
    private readonly HashSet<string> _gaveUp = [];

    /// <summary>Completes after the first poll cycle finishes. Intended for tests.</summary>
    public Task FirstCycleCompleted => _firstCycleCompleted.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;

            try
            {
                if (opts.Enabled)
                {
                    await PollOnceAsync(opts, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime-state poll cycle failed");
            }
            finally
            {
                _firstCycleCompleted.TrySetResult();
            }

            var delay = TimeSpan.FromSeconds(Math.Max(1, opts.PollIntervalSeconds));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollOnceAsync(AcRuntimeStateOptions opts, CancellationToken stoppingToken)
    {
        var window = TimeSpan.FromMinutes(Math.Max(1, opts.OnlineWindowMinutes));
        var macs = _deviceManager.GetRuntimeStatePollTargets(
            window,
            TimeSpan.FromSeconds(Math.Max(0, opts.FailureBackoffSeconds)),
            opts.MaxConsecutiveFailures);

        // Log once per device when it has failed enough times to be dropped for good
        // (distinct from a device merely inside its post-failure backoff window).
        // Forget devices that came back so a later outage is logged again.
        if (opts.MaxConsecutiveFailures > 0)
        {
            var recent = new HashSet<string>(_deviceManager.GetRecentlyConnectedMacs(window));
            var notGivenUp = new HashSet<string>(
                _deviceManager.GetRuntimeStatePollTargets(window, TimeSpan.Zero, opts.MaxConsecutiveFailures));

            _gaveUp.RemoveWhere(mac => !recent.Contains(mac) || notGivenUp.Contains(mac));
            foreach (var mac in recent)
            {
                if (!notGivenUp.Contains(mac) && _gaveUp.Add(mac))
                {
                    _logger.LogInformation(
                        "Stopped runtime-state polling {Mac} after {Failures} consecutive failures; will retry when it reconnects",
                        mac, opts.MaxConsecutiveFailures);
                }
            }
        }

        if (macs.Count == 0)
        {
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, opts.MaxDegreeOfParallelism),
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(macs, parallelOptions, async (mac, ct) =>
        {
            try
            {
                await _deviceManager.RefreshRuntimeStateAsync(mac, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // shutting down
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Runtime-state poll failed for {Mac}", mac);
            }
        });
    }
}
