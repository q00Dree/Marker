using Marker.Client.Output;
using Xunit;

namespace Marker.Tests.Client;

public class FileMarkingCodeOutputTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "marker-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task WriteAsync_WritesEachCodeOnItsOwnLine()
    {
        // Arrange
        var path = Path.Combine(_dir, "codes.txt");
        await using (var output = new FileMarkingCodeOutput(path))
        {
            // Act
            await output.WriteAsync("AAA", CancellationToken.None);
            await output.WriteAsync("BBB", CancellationToken.None);
        }

        // Assert
        Assert.Equal("AAA\nBBB\n", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Constructor_ExistingFile_AppendsInsteadOfOverwriting()
    {
        // Arrange
        var path = Path.Combine(_dir, "codes.txt");
        await using (var first = new FileMarkingCodeOutput(path))
            await first.WriteAsync("AAA", CancellationToken.None);

        // Act
        await using (var second = new FileMarkingCodeOutput(path))
            await second.WriteAsync("BBB", CancellationToken.None);

        // Assert
        Assert.Equal("AAA\nBBB\n", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriteAsync_CodeVisibleBeforeDispose()
    {
        // Arrange
        var path = Path.Combine(_dir, "codes.txt");
        await using var output = new FileMarkingCodeOutput(path);

        // Act
        await output.WriteAsync("AAA", CancellationToken.None);

        // Assert
        await using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var text = new StreamReader(reader);
        Assert.Equal("AAA\n", await text.ReadToEndAsync());
    }
}
