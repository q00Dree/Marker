using GS1Marker.Common.Protocol.Line;
using System.Text;
using Xunit;

namespace GS1Marker.Tests.Protocol.Line;

public class LineMarkingCodeWriterTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task WriteAsync_AppendsSingleLineFeed()
    {
        using var stream = new MemoryStream();
        using var writer = new LineMarkingCodeWriter(stream);

        await writer.WriteAsync("ABC", Ct);

        Assert.Equal("ABC\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_DoesNotEmitBom()
    {
        using var stream = new MemoryStream();
        using var writer = new LineMarkingCodeWriter(stream);

        await writer.WriteAsync("ABC", Ct);

        Assert.False(
            stream.ToArray().AsSpan().StartsWith(Encoding.UTF8.GetPreamble()),
            "Stream must not start with UTF-8 BOM.");
    }

    [Fact]
    public async Task WriteAsync_FlushesImmediately()
    {
        // Данные должны быть в потоке сразу после вызова, без Dispose.
        using var stream = new MemoryStream();
        using var writer = new LineMarkingCodeWriter(stream);

        await writer.WriteAsync("ABC", Ct);

        Assert.Equal(4, stream.Length);
    }

    [Fact]
    public async Task WriteAsync_MultipleCodes_AreWrittenInOrder()
    {
        using var stream = new MemoryStream();
        using var writer = new LineMarkingCodeWriter(stream);

        await writer.WriteAsync("AAA", Ct);
        await writer.WriteAsync("BBB", Ct);

        Assert.Equal("AAA\nBBB\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_Throws()
    {
        using var stream = new MemoryStream();
        using var writer = new LineMarkingCodeWriter(stream);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync("ABC", cts.Token));
    }

    [Fact]
    public void Constructor_NullStream_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new LineMarkingCodeWriter(null!));

    [Fact]
    public void Dispose_DoesNotCloseUnderlyingStream()
    {
        var stream = new MemoryStream();
        var writer = new LineMarkingCodeWriter(stream);

        writer.Dispose();

        Assert.True(stream.CanWrite);
    }
}