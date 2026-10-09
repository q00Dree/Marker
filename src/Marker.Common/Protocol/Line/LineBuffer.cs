using System.Diagnostics.CodeAnalysis;

namespace Marker.Common.Protocol.Line;

internal sealed class LineBuffer
{
    private const byte Lf = (byte)LineMarkingCodeFormat.Delimiter;
    private const byte Cr = (byte)LineMarkingCodeFormat.OptionalCarriageReturn;
    private const int ReadChunkSize = 4096;

    private readonly int _maxLineBytes;
    private readonly byte[] _data;
    private int _start;
    private int _end;

    public LineBuffer(int maxLineBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineBytes);

        _maxLineBytes = maxLineBytes;
        _data = new byte[maxLineBytes + 1 + ReadChunkSize];
    }

    public int PendingBytes => _end - _start;
    public bool HasPendingBytes => _end > _start;

    public bool TryTakeLine([NotNullWhen(true)] out string? line)
    {
        while (true)
        {
            var pending = _data.AsSpan(_start, _end - _start);
            var delimiter = pending.IndexOf(Lf);

            if (delimiter < 0)
            {
                if (pending.Length > _maxLineBytes + 1) throw LineTooLong();

                line = null;
                return false;
            }

            var content = TrimTrailingCr(pending[..delimiter]);
            if (content.Length > _maxLineBytes) throw LineTooLong();

            if (content.Length > 0)
            {
                line = LineMarkingCodeFormat.Encoding.GetString(content);
                _start += delimiter + 1;
                return true;
            }

            _start += delimiter + 1;
        }
    }

    public Memory<byte> GetFreeSpace()
    {
        if (_start > 0)
        {
            _data.AsSpan(_start, _end - _start).CopyTo(_data);
            _end -= _start;
            _start = 0;
        }

        return _data.AsMemory(_end);
    }

    public void Advance(int count) => _end += count;

    private static ReadOnlySpan<byte> TrimTrailingCr(ReadOnlySpan<byte> line) =>
        line is [.., Cr] ? line[..^1] : line;

    private InvalidDataException LineTooLong() =>
        new($"Message exceeds the maximum length of {_maxLineBytes} bytes.");
}