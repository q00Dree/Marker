namespace Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeWriter : IMarkingCodeWriter
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _gate;

    public LineMarkingCodeWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _writer = new StreamWriter(stream, LineMarkingCodeFormat.Encoding, leaveOpen: true)
        {
            NewLine = LineMarkingCodeFormat.Delimiter
        };
        _gate = new SemaphoreSlim(1, 1);
    }

    public async Task WriteAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        await _gate.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

            await _writer.WriteLineAsync(code.AsMemory(), ct);
            await _writer.FlushAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    #region Disposable
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await _gate.WaitAsync();
        try
        {
            await _writer.DisposeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
    #endregion
}