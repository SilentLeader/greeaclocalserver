using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace GreeACLocalServer.UI.Services;

public sealed class LinearBackoffRetryPolicy : IRetryPolicy
{
    private readonly int _initialSeconds;
    private readonly int _incrementSeconds;
    private readonly int _maxSeconds;

    public LinearBackoffRetryPolicy(int initialSeconds = 5, int incrementSeconds = 5, int maxSeconds = 60)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(incrementSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSeconds);
        _initialSeconds = initialSeconds;
        _incrementSeconds = incrementSeconds;
        _maxSeconds = maxSeconds;
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // retryContext.PreviousRetryCount is 0 on first reconnect attempt
        var attempt = retryContext.PreviousRetryCount;
        var seconds = _initialSeconds + (attempt * _incrementSeconds);
        if (seconds > _maxSeconds)
            seconds = _maxSeconds;
        return TimeSpan.FromSeconds(seconds);
    }
}
