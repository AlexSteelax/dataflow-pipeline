namespace Steelax.DataflowPipeline.UnitTests.Common;

public class TimedAvailabilityAdvancedTests(ITestOutputHelper output)
{
    /// <summary>
    /// Verifies behavior with null values for reference types
    /// </summary>
    [Fact]
    public void Available_WithNullValue_CreatesValidInstance()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var result = TimedAvailability<string?>.Available(null, timestamp);

        // Assert
        Assert.True(result.IsAvailable);
        Assert.Null(result.Value);
        
        output.WriteLine("Available instance with null value created successfully");
    }

    /// <summary>
    /// Verifies hash code consistency for equal instances
    /// </summary>
    [Fact]
    public void GetHashCode_ForEqualInstances_ReturnsSameValue()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var instance1 = TimedAvailability<int>.Available(42, timestamp);
        var instance2 = TimedAvailability<int>.Available(42, timestamp);

        // Act & Assert
        Assert.Equal(instance1.GetHashCode(), instance2.GetHashCode());
    }

    /// <summary>
    /// Tests edge case with minimum timestamp value
    /// </summary>
    [Fact]
    public void DefaultConstructor_CreatesTimeout_WithMinimalTimestamp()
    {
        // Arrange & Act
        var result = new TimedAvailability<int>();

        // Assert
        Assert.False(result.IsAvailable);
        Assert.Equal(DateTimeOffset.MinValue, result.Timestamp);
        
        output.WriteLine("Default constructor creates timeout with minimal timestamp");
    }
}