namespace Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeReader : IMarkingCodeReader
{
    private readonly Stream _stream;
    private readonly LineBuffer _buffer;
    private readonly SemaphoreSlim _gate;

    public LineMarkingCodeReader(Stream stream, int maxLineBytes = LineMarkingCodeFormat.DefaultMaxLineBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _buffer = new LineBuffer(maxLineBytes);
        _gate = new SemaphoreSlim(1, 1);
    }

    public async Task<string?> ReadAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposing.Token);

        try
        {
            await _gate.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        try
        {
            return await ReadLineAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        while (true)
        {
            if (_buffer.TryTakeLine(out var line)) return line;

            var read = await _stream.ReadAsync(_buffer.GetFreeSpace(), ct);
            if (read == 0) return EndOfStream();

            _buffer.Advance(read);
        }
    }

    private string? EndOfStream() =>
        _buffer.HasPendingBytes
            ? throw new InvalidDataException(
                $"Connection closed in the middle of a message ({_buffer.PendingBytes} bytes without delimiter).")
            : null;

    #region Disposable
    private readonly CancellationTokenSource _disposing = new();
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await _disposing.CancelAsync();
        await _gate.WaitAsync();
        _gate.Release();
    }
    #endregion
}