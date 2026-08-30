namespace GreeACLocalServer.Device.Services;

/// <summary>
/// A read-only stream that first replays an in-memory <paramref name="prefix"/>
/// and then transparently forwards to <paramref name="inner"/>. Used to "un-read"
/// the byte(s) peeked from a client connection before handing the stream to a
/// <see cref="StreamReader"/>. Does not own <paramref name="inner"/> and never
/// closes it.
/// </summary>
internal sealed class PrefixedReadStream(ReadOnlyMemory<byte> prefix, Stream inner) : Stream
{
    private ReadOnlyMemory<byte> _prefix = prefix;
    private readonly Stream _inner = inner;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (!_prefix.IsEmpty)
        {
            var n = Math.Min(_prefix.Length, buffer.Length);
            _prefix.Span[..n].CopyTo(buffer);
            _prefix = _prefix[n..];
            return n;
        }

        return _inner.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!_prefix.IsEmpty)
        {
            var n = Math.Min(_prefix.Length, buffer.Length);
            _prefix.Span[..n].CopyTo(buffer.Span);
            _prefix = _prefix[n..];
            return n;
        }

        return await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
