namespace Marker.Tests.Protocol;

internal sealed class ChunkedStream : Stream
{
    private int _index;
    private readonly byte[][] _chunks;

    public ChunkedStream(params byte[][] chunks)
    {
        _chunks = chunks;
    }

    public override int Read(Span<byte> buffer)
    {
        if (_index >= _chunks.Length)
            return 0;

        var chunk = _chunks[_index++];
        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}