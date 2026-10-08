using Marker.Common.Protocol.Line;
using System.Text;
using Xunit;

namespace Marker.Tests.Protocol.Line;

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
    public async Task WriteAsync_Concurrent_LinesAreNotInterleaved()
    {
        // Arrange
        const int count = 200;
        using var stream = new MemoryStream();
        await using var writer = new LineMarkingCodeWriter(stream);
        var codes = Enumerable.Range(0, count).Select(i => new string((char)('A' + i % 26), 50) + i).ToArray();

        // Act
        await Task.WhenAll(codes.Select(c => Task.Run(() => writer.WriteAsync(c, Ct))));

        // Assert
        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(codes.Order(), lines.Order());
    }

    [Fact]
    public async Task WriteAsync_AfterDispose_ThrowsObjectDisposed()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LineMarkingCodeWriter(stream);
        await writer.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => writer.WriteAsync("ABC", Ct));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LineMarkingCodeWriter(stream);

        // Act
        await writer.DisposeAsync();
        var exception = await Record.ExceptionAsync(() => writer.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_Concurrent_DoesNotThrow()
    {
        // Arrange
        using var stream = new MemoryStream();
        var writer = new LineMarkingCodeWriter(stream);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => writer.DisposeAsync().AsTask()))));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForInFlightWrite()
    {
        // Arrange
        var stream = new GatedWriteStream();
        var writer = new LineMarkingCodeWriter(stream);
        var write = writer.WriteAsync("ABC", Ct);
        await stream.WriteStarted;

        // Act
        var dispose = writer.DisposeAsync().AsTask();
        await Task.Delay(50);
        var completedBeforeRelease = dispose.IsCompleted;
        stream.Release();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await write;

        // Assert
        Assert.False(completedBeforeRelease);
        Assert.Equal("ABC\n"u8.ToArray(), stream.Written);
    }

    [Fact]
    public async Task DisposeAsync_QueuedWriteAfterDispose_ThrowsObjectDisposed()
    {
        // Arrange
        var stream = new GatedWriteStream();
        var writer = new LineMarkingCodeWriter(stream);
        var first = writer.WriteAsync("AAA", Ct);
        await stream.WriteStarted;
        var dispose = writer.DisposeAsync().AsTask();
        var late = writer.WriteAsync("BBB", Ct);

        // Act
        stream.Release();
        await dispose.WaitAsync(TimeSpan.FromSeconds(5));
        await first;

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => late);
        Assert.Equal("AAA\n"u8.ToArray(), stream.Written);
    }

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