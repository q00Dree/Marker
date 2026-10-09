using System.Text;

namespace Marker.Client.Output;

public sealed class FileMarkingCodeOutput : IAsyncDisposable
{
    private readonly StreamWriter _writer;

    public FileMarkingCodeOutput(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
    }

    public async Task WriteAsync(string code, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await _writer.WriteLineAsync(code.AsMemory(), ct);
        await _writer.FlushAsync(ct);
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();
}