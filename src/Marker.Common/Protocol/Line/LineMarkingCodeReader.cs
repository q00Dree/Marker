namespace Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeReader : IMarkingCodeReader
{
    private readonly StreamReader _reader;
    private readonly SemaphoreSlim _gate;

    public LineMarkingCodeReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _reader = new StreamReader(stream, LineMarkingCodeFormat.Encoding, false, leaveOpen: true);
        _gate = new SemaphoreSlim(1, 1);
    }

    public async Task<string?> ReadAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);

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
            while (true)
            {
                var line = await _reader.ReadLineAsync(linked.Token);
                if (line is null) return null;
                if (line.Length > 0) return line;
            }
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

    #region Disposable
    private readonly CancellationTokenSource _disposeCts = new();
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await _disposeCts.CancelAsync();
        await _gate.WaitAsync();
        try
        {
            _reader.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }
    #endregion
}