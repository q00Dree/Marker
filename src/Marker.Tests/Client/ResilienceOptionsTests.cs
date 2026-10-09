using Marker.Client.Networking;
using Xunit;

namespace Marker.Tests.Client;

public class ResilienceOptionsTests
{
    private static ResilienceOptions Options(double multiplier, int? maxAttempts = null) => new()
    {
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Multiplier = multiplier,
        MaxAttempts = maxAttempts
    };

    [Fact]
    public void GetDelay_Multiplier2_DoublesEachAttempt()
    {
        // Arrange
        var options = Options(2);

        // Act & Assert
        Assert.Equal(TimeSpan.FromSeconds(1), options.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(2), options.GetDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(4), options.GetDelay(3));
    }

    [Fact]
    public void GetDelay_Multiplier1_IsFixed()
    {
        // Arrange
        var options = Options(1);

        // Act & Assert
        Assert.Equal(TimeSpan.FromSeconds(1), options.GetDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(1), options.GetDelay(50));
    }

    [Fact]
    public void GetDelay_GrowthExceedsMax_IsClamped()
    {
        // Arrange
        var options = Options(2);

        // Act & Assert
        Assert.Equal(TimeSpan.FromSeconds(10), options.GetDelay(5));
        Assert.Equal(TimeSpan.FromSeconds(10), options.GetDelay(5000));
    }

    [Fact]
    public void GetDelay_AfterMaxAttempts_ReturnsNull()
    {
        // Arrange
        var options = Options(2, maxAttempts: 2);

        // Act & Assert
        Assert.NotNull(options.GetDelay(2));
        Assert.Null(options.GetDelay(3));
    }

    [Fact]
    public void GetDelay_MaxAttemptsZero_NeverRetries()
    {
        // Arrange
        var options = Options(2, maxAttempts: 0);

        // Act & Assert
        Assert.Null(options.GetDelay(1));
    }

    [Fact]
    public void GetDelay_AttemptBelowOne_Throws()
    {
        // Arrange
        var options = Options(2);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.GetDelay(0));
    }
}
