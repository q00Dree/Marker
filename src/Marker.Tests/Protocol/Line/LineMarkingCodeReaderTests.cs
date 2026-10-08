using Marker.Common.Protocol.Line;
using System.Text;
using Xunit;

namespace Marker.Tests.Protocol.Line;

public class LineMarkingCodeReaderTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static LineMarkingCodeReader ReaderFor(string text) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public async Task ReadAsync_SingleMessage_ReturnsCode()
    {
        // Arrange
        using var reader = ReaderFor("AAA\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_ConcatenatedMessages_AreSplitCorrectly()
    {
        // Arrange
        using var reader = ReaderFor("AAA\nBBB\nCCC\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Equal("CCC", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_MessageSplitAcrossChunks_IsAssembled()
    {
        // Arrange
        using var reader = new LineMarkingCodeReader(
            new ChunkedStream("ABC"u8.ToArray(), "DEF\nGH"u8.ToArray(), "I\n"u8.ToArray()));

        // Act & Assert
        Assert.Equal("ABCDEF", await reader.ReadAsync(Ct));
        Assert.Equal("GHI", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_ByteByByte_IsAssembled()
    {
        // Arrange
        var chunks = Encoding.UTF8.GetBytes("HELLO\nWORLD\n").Select(b => new[] { b }).ToArray();
        using var reader = new LineMarkingCodeReader(new ChunkedStream(chunks));

        // Act & Assert
        Assert.Equal("HELLO", await reader.ReadAsync(Ct));
        Assert.Equal("WORLD", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CrLfDelimiter_IsAccepted()
    {
        // Arrange
        using var reader = ReaderFor("AAA\r\nBBB\r\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyLines_AreSkipped()
    {
        // Arrange
        using var reader = ReaderFor("\n\nAAA\n\nBBB\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ReturnsNull()
    {
        // Arrange
        using var reader = ReaderFor("");

        // Act & Assert
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_LastMessageWithoutDelimiter_IsReturnedBeforeEof()
    {
        // Arrange
        using var reader = ReaderFor("AAA\nBBB");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_Throws()
    {
        // Arrange
        using var reader = ReaderFor("AAA\n");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(cts.Token));
    }

    [Fact]
    public void Constructor_NullStream_Throws() =>
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LineMarkingCodeReader(null!));

    [Fact]
    public async Task Dispose_DoesNotCloseUnderlyingStream()
    {
        // Arrange
        var stream = new MemoryStream("AAA\n"u8.ToArray());
        var reader = new LineMarkingCodeReader(stream);
        await reader.ReadAsync(Ct);

        // Act
        reader.Dispose();

        // Assert
        Assert.True(stream.CanRead);
    }
}