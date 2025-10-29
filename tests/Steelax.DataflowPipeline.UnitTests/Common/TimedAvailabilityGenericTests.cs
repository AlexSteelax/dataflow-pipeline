using System;
using Xunit;

namespace Steelax.DataflowPipeline.UnitTests.Common;

public class TimedAvailabilityGenericTests
{
    /// <summary>
    /// Verifies that Available factory method creates instance with correct properties
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData("test")]
    public void Available_CreatesInstance_WithCorrectProperties<T>(T value)
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = TimedAvailability<T>.Available(value, timestamp);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Equal(timestamp, result.Timestamp);
        Assert.Equal(value, result.Value);
    }

    /// <summary>
    /// Verifies that Timeout factory method creates instance with correct properties
    /// </summary>
    [Fact]
    public void Timeout_CreatesInstance_WithCorrectProperties()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = TimedAvailability<int>.Timeout(timestamp);

        // Assert
        Assert.False(result.IsAvailable);
        Assert.Equal(timestamp, result.Timestamp);
    }

    /// <summary>
    /// Verifies that Value property throws InvalidOperationException when accessed on timeout instance
    /// </summary>
    [Fact]
    public void Value_WhenTimeout_ThrowsInvalidOperationException()
    {
        // Arrange
        var timeout = TimedAvailability<int>.Timeout(DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => timeout.Value);
    }

    /// <summary>
    /// Verifies that WithTimestamp method preserves availability state and value while updating timestamp
    /// </summary>
    [Fact]
    public void WithTimestamp_UpdatesTimestamp_PreservesStateAndValue()
    {
        // Arrange
        var originalTimestamp = DateTimeOffset.UtcNow;
        var newTimestamp = originalTimestamp.AddMinutes(1);
        var original = TimedAvailability<int>.Available(42, originalTimestamp);

        // Act
        var result = original.WithTimestamp(newTimestamp);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Equal(newTimestamp, result.Timestamp);
        Assert.Equal(42, result.Value);
    }

    /// <summary>
    /// Verifies that two available instances with same timestamp and value are considered equal
    /// </summary>
    [Theory]
    [InlineData(42, 42, true)]
    [InlineData(42, 100, false)]
    public void Equals_ForAvailableInstances_ReturnsExpectedResult(int value1, int value2, bool expectedEqual)
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var instance1 = TimedAvailability<int>.Available(value1, timestamp);
        var instance2 = TimedAvailability<int>.Available(value2, timestamp);

        // Act & Assert
        Assert.Equal(expectedEqual, instance1.Equals(instance2));
    }

    /// <summary>
    /// Verifies that available and timeout instances are never considered equal
    /// </summary>
    [Fact]
    public void Equals_ForAvailableAndTimeoutInstances_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var available = TimedAvailability<int>.Available(42, timestamp);
        var timeout = TimedAvailability<int>.Timeout(timestamp);

        // Act & Assert
        Assert.False(available.Equals(timeout));
    }
}