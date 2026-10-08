namespace GS1Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeWriter : IMarkingCodeWriter
{
    private readonly StreamWriter _writer;

    public LineMarkingCodeWriter(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _writer = new StreamWriter(stream, LineMarkingCodeFormat.Encoding, leaveOpen: true)
        {
            NewLine = LineMarkingCodeFormat.Delimiter
        };
    }

    public async Task WriteAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await _writer.WriteLineAsync(code.AsMemory(), ct);
        await _writer.FlushAsync(ct);
    }

    public void Dispose() => _writer.Dispose();
}