using System.Text;
using GreeACLocalServer.Device.Services;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Guards WP-04 finding F5: <see cref="ClientLineReader"/> must bound a read with
/// an idle timeout (because <c>StreamReader.ReadLineAsync</c> ignores the socket
/// receive timeout) and must reject over-long lines.
/// </summary>
public class ClientLineReaderTests
{
    [Fact]
    public async Task ReadLineAsync_ReturnsLine_WhenDataAvailable()
    {
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes("{\"t\":\"heartbeat\"}\n")));

        var result = await ClientLineReader.ReadLineAsync(
            reader, TimeSpan.FromSeconds(5), 1024, CancellationToken.None);

        Assert.Equal(ClientLineReader.ReadOutcome.Line, result.Outcome);
        Assert.Equal("{\"t\":\"heartbeat\"}", result.Line);
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsClosed_OnEndOfStream()
    {
        using var reader = new StreamReader(new MemoryStream(Array.Empty<byte>()));

        var result = await ClientLineReader.ReadLineAsync(
            reader, TimeSpan.FromSeconds(5), 1024, CancellationToken.None);

        Assert.Equal(ClientLineReader.ReadOutcome.Closed, result.Outcome);
        Assert.Null(result.Line);
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsLineTooLong_WhenLineExceedsLimit()
    {
        var line = new string('a', 50) + "\n";
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(line)));

        var result = await ClientLineReader.ReadLineAsync(
            reader, TimeSpan.FromSeconds(5), maxLineLength: 16, CancellationToken.None);

        Assert.Equal(ClientLineReader.ReadOutcome.LineTooLong, result.Outcome);
        Assert.Null(result.Line);
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsIdleTimeout_WhenNoDataArrives()
    {
        using var reader = new StreamReader(new BlockingStream());

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await ClientLineReader.ReadLineAsync(
            reader, TimeSpan.FromMilliseconds(150), 1024, CancellationToken.None);
        sw.Stop();

        Assert.Equal(ClientLineReader.ReadOutcome.IdleTimeout, result.Outcome);
        Assert.True(sw.ElapsedMilliseconds >= 100, $"returned too early: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ReadLineAsync_PropagatesCancellation_OnShutdown()
    {
        using var reader = new StreamReader(new BlockingStream());
        using var shutdown = new CancellationTokenSource();
        shutdown.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ClientLineReader.ReadLineAsync(reader, TimeSpan.FromSeconds(30), 1024, shutdown.Token));
    }

    /// <summary>A stream whose reads never complete until cancelled.</summary>
    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
