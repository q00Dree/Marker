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
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.WriteAsync("ABC", Ct);

        // Assert
        Assert.Equal("ABC\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_DoesNotEmitBom()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.WriteAsync("ABC", Ct);

        // Assert
        Assert.False(
            stream.ToArray().AsSpan().StartsWith(Encoding.UTF8.GetPreamble()),
            "Stream must not start with UTF-8 BOM.");
    }

    [Fact]
    public async Task WriteAsync_FlushesImmediately()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.WriteAsync("ABC", Ct);

        // Assert
        Assert.Equal(4, stream.Length);
    }

    [Fact]
    public async Task WriteAsync_MultipleCodes_AreWrittenInOrder()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.WriteAsync("AAA", Ct);
        await writer.WriteAsync("BBB", Ct);

        // Assert
        Assert.Equal("AAA\nBBB\n"u8.ToArray(), stream.ToArray());
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_Throws()
    {
        // Arrange
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteAsync("ABC", cts.Token));
    }

    [Fact]
    public void Constructor_NullStream_Throws() =>
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LineMarkingCodeWriter(null!));

    [Fact]
    public async Task Dispose_DoesNotCloseUnderlyingStream()
    {
        // Arrange
        var stream = new MemoryStream();
        var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.DisposeAsync();

        // Assert
        Assert.True(stream.CanWrite);
    }
}