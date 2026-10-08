namespace Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeReader : IMarkingCodeReader
{
    private readonly StreamReader _reader;

    public LineMarkingCodeReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _reader = new StreamReader(stream, LineMarkingCodeFormat.Encoding, false, leaveOpen: true);
    }

    public async Task<string?> ReadAsync(CancellationToken ct)
    {
        while (true)
        {
            var line = await _reader.ReadLineAsync(ct);
            if (line is null) return null;
            if (line.Length > 0) return line;
        }
    }

    public void Dispose() => _reader.Dispose();
}