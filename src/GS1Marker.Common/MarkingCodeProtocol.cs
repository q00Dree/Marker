using System.Text;

namespace GS1Marker.Common;

public static class MarkingCodeProtocol
{
    private const string Delimiter = "\n";

    private static readonly UTF8Encoding Encoding =
        new(encoderShouldEmitUTF8Identifier: false);

    public static StreamReader CreateReader(Stream stream) =>
        new(stream, Encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);

    public static StreamWriter CreateWriter(Stream stream) =>
        new(stream, Encoding, leaveOpen: true) { NewLine = Delimiter };

    public static async Task WriteCodeAsync(TextWriter writer, string code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrEmpty(code);

        if (code.Contains('\n') || code.Contains('\r'))
            throw new ArgumentException("Code must not contain line breaks.", nameof(code));

        await writer.WriteAsync($"{code}{Delimiter}".AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    public static async Task<string?> ReadCodeAsync(TextReader reader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null) return null;
            if (line.Length > 0) return line;
        }
    }
}