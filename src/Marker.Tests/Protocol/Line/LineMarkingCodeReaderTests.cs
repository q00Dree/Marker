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
        await using var reader = ReaderFor("AAA\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_ConcatenatedMessages_AreSplitCorrectly()
    {
        // Arrange
        await using var reader = ReaderFor("AAA\nBBB\nCCC\n");

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
        await using var reader = new LineMarkingCodeReader(
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
        await using var reader = new LineMarkingCodeReader(new ChunkedStream(chunks));

        // Act & Assert
        Assert.Equal("HELLO", await reader.ReadAsync(Ct));
        Assert.Equal("WORLD", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CrLfDelimiter_IsAccepted()
    {
        // Arrange
        await using var reader = ReaderFor("AAA\r\nBBB\r\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyLines_AreSkipped()
    {
        // Arrange
        await using var reader = ReaderFor("\n\nAAA\n\nBBB\n");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        Assert.Equal("BBB", await reader.ReadAsync(Ct));
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ReturnsNull()
    {
        // Arrange
        await using var reader = ReaderFor("");

        // Act & Assert
        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_LastMessageWithoutDelimiter_ThrowsInvalidData()
    {
        // Arrange
        await using var reader = ReaderFor("AAA\nBBB");

        // Act & Assert
        Assert.Equal("AAA", await reader.ReadAsync(Ct));
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_LineAtLimit_IsAccepted()
    {
        // Arrange
        await using var reader = new LineMarkingCodeReader(
            new MemoryStream(Encoding.UTF8.GetBytes("ABCDE\r\nFGHIJ\n")), maxLineBytes: 5);

        // Act & Assert
        Assert.Equal("ABCDE", await reader.ReadAsync(Ct));
        Assert.Equal("FGHIJ", await reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_LineExceedsLimitWithDelimiter_ThrowsInvalidData()
    {
        // Arrange
        await using var reader = new LineMarkingCodeReader(
            new MemoryStream(Encoding.UTF8.GetBytes("ABCDEF\n")), maxLineBytes: 5);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_EndlessDataWithoutDelimiter_ThrowsInvalidData()
    {
        // Arrange
        await using var reader = new LineMarkingCodeReader(
            new MemoryStream(new byte[100_000]), maxLineBytes: 32);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_Throws()
    {
        // Arrange
        await using var reader = ReaderFor("AAA\n");
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
    public async Task ReadAsync_Concurrent_EachLineDeliveredExactlyOnce()
    {
        // Arrange
        const int count = 1000;
        var expected = Enumerable.Range(0, count).Select(i => $"CODE{i}").ToArray();
        await using var reader = ReaderFor(string.Concat(expected.Select(c => c + "\n")));
        var received = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (await reader.ReadAsync(Ct) is { } code) received.Add(code);
        })));

        // Assert
        Assert.Equal(expected.Order(), received.Order());
    }

    [Fact]
    public async Task ReadAsync_AfterDispose_ThrowsObjectDisposed()
    {
        // Arrange
        var reader = ReaderFor("AAA\n");
        await reader.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.ReadAsync(Ct));
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var reader = ReaderFor("AAA\n");

        // Act
        await reader.DisposeAsync();
        var exception = await Record.ExceptionAsync(() => reader.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_Concurrent_DoesNotThrow()
    {
        // Arrange
        var reader = ReaderFor("AAA\n");

        // Act
        var exception = await Record.ExceptionAsync(() =>
            Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => reader.DisposeAsync().AsTask()))));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_PendingRead_IsInterruptedWithObjectDisposed()
    {
        // Arrange
        var stream = new BlockingStream();
        var reader = new LineMarkingCodeReader(stream);
        var pending = reader.ReadAsync(Ct);
        await stream.ReadStarted;

        // Act
        await reader.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
    }

    [Fact]
    public async Task DisposeAsync_QueuedRead_IsInterruptedWithObjectDisposed()
    {
        // Arrange
        var stream = new BlockingStream();
        var reader = new LineMarkingCodeReader(stream);
        var first = reader.ReadAsync(Ct);
        await stream.ReadStarted;
        var queued = reader.ReadAsync(Ct);

        // Act
        await reader.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => first);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
    }

    [Fact]
    public async Task ReadAsync_CallerCancelsPendingRead_ThrowsOperationCanceled()
    {
        // Arrange
        var stream = new BlockingStream();
        await using var reader = new LineMarkingCodeReader(stream);
        using var cts = new CancellationTokenSource();
        var pending = reader.ReadAsync(cts.Token);
        await stream.ReadStarted;

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotCloseUnderlyingStream()
    {
        // Arrange
        var stream = new MemoryStream("AAA\n"u8.ToArray());
        var reader = new LineMarkingCodeReader(stream);
        await reader.ReadAsync(Ct);

        // Act
        await reader.DisposeAsync();

        // Assert
        Assert.True(stream.CanRead);
    }
}