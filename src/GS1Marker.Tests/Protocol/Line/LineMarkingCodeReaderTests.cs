using GS1Marker.Common.Protocol.Line;
using System.Text;
using Xunit;

namespace GS1Marker.Tests.Protocol.Line;

public class LineMarkingCodeReaderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static LineMarkingCodeReader ReaderFor(string text) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public async Task ReadAsync_SingleMessage_ReturnsCode()
    {
        using var reader = ReaderFor("AAA\n");

        Assert.Equal("AAA", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_ConcatenatedMessages_AreSplitCorrectly()
    {
        using var reader = ReaderFor("AAA\nBBB\nCCC\n");

        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Equal("CCC", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_MessageSplitAcrossChunks_IsAssembled()
    {
        using var reader = new LineMarkingCodeReader(
            new ChunkedStream("ABC"u8.ToArray(), "DEF\nGH"u8.ToArray(), "I\n"u8.ToArray()));

        Assert.Equal("ABCDEF", await reader.ReadAsync(Ct));
        Assert.Equal("GHI", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_ByteByByte_IsAssembled()
    {
        var chunks = Encoding.UTF8.GetBytes("HELLO\nWORLD\n").Select(b => new[] { b }).ToArray();
        using var reader = new LineMarkingCodeReader(new ChunkedStream(chunks));

        Assert.Equal("HELLO", await reader.ReadAsync(Ct));
        Assert.Equal("WORLD", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CrLfDelimiter_IsAccepted()
    {
        using var reader = ReaderFor("AAA\r\nBBB\r\n");

        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyLines_AreSkipped()
    {
        using var reader = ReaderFor("\n\nAAA\n\nBBB\n");

        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ReturnsNull()
    {
        using var reader = ReaderFor("");

        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_LastMessageWithoutDelimiter_IsReturnedBeforeEof()
    {
        // Известное ограничение формата: обрезанный хвост неотличим от целого сообщения.
        using var reader = ReaderFor("AAA\nBBB");

        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_Throws()
    {
        using var reader = ReaderFor("AAA\n");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cts.Token));
    }

    [Fact]
    public void Constructor_NullStream_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new LineMarkingCodeReader(null!));

    [Fact]
    public async Task Dispose_DoesNotCloseUnderlyingStream()
    {
        var stream = new MemoryStream("AAA\n"u8.ToArray());
        var reader = new LineMarkingCodeReader(stream);
        await reader.ReadAsync(Ct);

        reader.Dispose();

        Assert.True(stream.CanRead);
    }
}