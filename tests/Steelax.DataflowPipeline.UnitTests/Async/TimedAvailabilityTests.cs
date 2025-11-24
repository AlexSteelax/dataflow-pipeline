namespace Steelax.DataflowPipeline.UnitTests.Async;

public class TimedAvailabilityTests
{
    [Fact]
    public void Available_ShouldCreateCorrectStructure()
    {
        // Arrange
        var value = 42;
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = TimedAvailability<int>.Available(value, timestamp);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Equal(value, result.Value);
        Assert.Equal(timestamp, result.Timestamp);
    }

    [Fact]
    public void Timeout_ShouldCreateCorrectStructure()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = TimedAvailability<int>.Timeout(timestamp);

        // Assert
        Assert.False(result.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Equal(timestamp, result.Timestamp);
    }

    [Fact]
    public void Available_ShouldThrowOnAccessValue_WhenTimeout()
    {
        // Arrange
        var timeout = TimedAvailability<int>.Timeout(DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => timeout.Value);
    }
}