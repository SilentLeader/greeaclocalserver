namespace GreeACLocalServer.Device.Services;

/// <summary>
/// Reads a single protocol line while enforcing an idle read-timeout and a
/// maximum line length. Extracted from <see cref="SocketHandlerService"/> so the
/// timeout / oversize logic is unit-testable without a real socket.
/// </summary>
internal static class ClientLineReader
{
    internal enum ReadOutcome
    {
        /// <summary>A complete line was read.</summary>
        Line,

        /// <summary>The peer closed the connection (end of stream).</summary>
        Closed,

        /// <summary>No data arrived within the idle timeout.</summary>
        IdleTimeout,

        /// <summary>The line exceeded <see cref="ValueObjects.ServerOption.MaxLineLength"/>.</summary>
        LineTooLong,
    }

    internal readonly record struct ReadResult(ReadOutcome Outcome, string? Line);

    /// <summary>
    /// Reads one line from <paramref name="reader"/>. A linked
    /// <see cref="CancellationTokenSource"/> with <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>
    /// bounds the wait, because <see cref="StreamReader.ReadLineAsync(CancellationToken)"/>
    /// ignores the underlying socket receive timeout.
    /// </summary>
    /// <param name="idleTimeout">
    /// Maximum time to wait for data. <see cref="Timeout.InfiniteTimeSpan"/> (or any
    /// non-positive value) disables the timeout.
    /// </param>
    /// <param name="shutdownToken">
    /// Server shutdown token. When it is cancelled the cancellation propagates to
    /// the caller instead of being reported as an idle timeout.
    /// </param>
    internal static async Task<ReadResult> ReadLineAsync(
        TextReader reader,
        TimeSpan idleTimeout,
        int maxLineLength,
        CancellationToken shutdownToken)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken);
        if (idleTimeout > TimeSpan.Zero)
        {
            readCts.CancelAfter(idleTimeout);
        }

        string? data;
        try
        {
            data = await reader.ReadLineAsync(readCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!shutdownToken.IsCancellationRequested)
        {
            return new ReadResult(ReadOutcome.IdleTimeout, null);
        }

        if (data is null)
        {
            return new ReadResult(ReadOutcome.Closed, null);
        }

        if (data.Length > maxLineLength)
        {
            return new ReadResult(ReadOutcome.LineTooLong, null);
        }

        return new ReadResult(ReadOutcome.Line, data);
    }
}
