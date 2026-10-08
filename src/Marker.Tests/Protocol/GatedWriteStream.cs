namespace Marker.Tests.Protocol;

internal sealed class GatedWriteStream : Stream
{
    private readonly MemoryStream _inner = new();
    private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WriteStarted => _writeStarted.Task;

    public byte[] Written => _inner.ToArray();

    public void Release() => _gate.TrySetResult();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _writeStarted.TrySetResult();
        await _gate.Task;
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override void Flush() { }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}