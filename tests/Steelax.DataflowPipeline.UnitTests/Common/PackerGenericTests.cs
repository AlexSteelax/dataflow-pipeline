namespace Steelax.DataflowPipeline.UnitTests.Common;

public class PackerGenericTests
{
    /// <summary>
    /// Verifies that packer initializes correctly with valid capacity and starts in empty state
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void Constructor_WithValidCapacity_CreatesEmptyPacker(int capacity)
    {
        // Arrange & Act
        var packer = new Packer<int>(capacity);

        // Assert
        Assert.True(packer.IsEmpty);
    }

    /// <summary>
    /// Ensures that constructor throws ArgumentOutOfRangeException for invalid capacity values
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Packer<int>(invalidCapacity));
    }

    /// <summary>
    /// Tests that items can be added to buffer without batch creation when buffer is not full
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void TryEnqueue_WhenBufferNotFull_AddsItemWithoutCreatingBatch(int capacity)
    {
        // Arrange
        var packer = new Packer<int>(capacity);

        // Act & Assert - Add items up to capacity-1
        for (var i = 0; i < capacity - 1; i++)
        {
            var result = packer.TryEnqueue(i, out var batch);

            Assert.False(result);
            Assert.Null(batch);
            Assert.False(packer.IsEmpty);
        }
    }

    /// <summary>
    /// Verifies that batch is created when buffer becomes full after adding an item
    /// </summary>
    [Theory]
    [InlineData(1, 42)]
    [InlineData(3, 100)]
    [InlineData(5, -1)]
    public void TryEnqueue_WhenBufferBecomesFull_CreatesBatchAndClearsBuffer(int capacity, int testValue)
    {
        // Arrange
        var packer = new Packer<int>(capacity);

        // Fill buffer to capacity-1
        for (var i = 0; i < capacity - 1; i++)
        {
            packer.TryEnqueue(i, out _);
        }

        // Act
        var result = packer.TryEnqueue(testValue, out var batch);

        // Assert
        Assert.True(result);
        Assert.NotNull(batch);
        Assert.True(packer.IsEmpty);
    }

    /// <summary>
    /// Tests that TryClear returns false and no batch when packer is empty
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void TryClear_WhenEmpty_ReturnsFalseAndNullBatch(int capacity)
    {
        // Arrange
        var packer = new Packer<int>(capacity);

        // Act
        var result = packer.TryClear(out var batch);

        // Assert
        Assert.False(result);
        Assert.Null(batch);
        Assert.True(packer.IsEmpty);
    }

    /// <summary>
    /// Verifies that TryClear creates batch with all buffered items and clears the buffer
    /// </summary>
    [Theory]
    [InlineData(2, new[] { 42 })]
    [InlineData(3, new[] { 1, 2 })]
    [InlineData(5, new[] { 10, 20, 30, 40 })]
    public void TryClear_WithBufferedItems_CreatesBatchAndClearsBuffer(int capacity, int[] items)
    {
        // Arrange
        var packer = new Packer<int>(capacity);
        foreach (var item in items)
        {
            packer.TryEnqueue(item, out _);
        }

        // Act
        var result = packer.TryClear(out var batch);

        // Assert
        Assert.True(result);
        Assert.NotNull(batch);
        Assert.True(packer.IsEmpty);
        Assert.Equal(items.Length, batch.Count);
    }
    
    /// <summary>
    /// Tests that TryClear returns false when used after TryEnqueue with capacity 1, 
    /// since each TryEnqueue immediately creates a batch and clears the buffer
    /// </summary>
    [Fact]
    public void TryClear_WithCapacityOne_ReturnsFalseAfterTryEnqueue()
    {
        // Arrange
        var packer = new Packer<int>(1);

        // Act - Add item which immediately creates batch and clears buffer
        packer.TryEnqueue(42, out var enqueueBatch);
    
        // Try to clear after enqueue - buffer should be empty
        var result = packer.TryClear(out var clearBatch);

        // Assert
        Assert.True(enqueueBatch != null); // Batch was created by TryEnqueue
        Assert.False(result); // TryClear should return false since buffer is empty
        Assert.Null(clearBatch); // No batch from TryClear
        Assert.True(packer.IsEmpty);
    }

    /// <summary>
    /// Tests multiple batch creations when continuously adding items beyond buffer capacity
    /// </summary>
    [Theory]
    [InlineData(2, new[] { 1, 2, 3, 4 })]
    [InlineData(3, new[] { 10, 20, 30, 40, 50 })]
    public void TryEnqueue_MultipleItemsBeyondCapacity_CreatesMultipleFullBatches(int capacity, int[] items)
    {
        // Arrange
        var packer = new Packer<int>(capacity);
        var batchCount = 0;

        // Act
        foreach (var item in items)
        {
            if (packer.TryEnqueue(item, out var batch))
            {
                batchCount++;
                Assert.NotNull(batch);
                Assert.Equal(capacity, batch.Count);
            }
        }

        // Assert
        var expectedFullBatches = items.Length / capacity;
        Assert.Equal(expectedFullBatches, batchCount);
    }

    /// <summary>
    /// Tests that when adding items sequentially, batch is created only when buffer becomes full
    /// and buffer is cleared after batch creation
    /// </summary>
    [Fact]
    public void TryEnqueue_WhenAddingItemsSequentially_CreatesBatchOnlyWhenBufferFull()
    {
        // Arrange - Use capacity > 1 to make this scenario achievable
        const int capacity = 3;
        var packer = new Packer<int>(capacity);

        // Act & Assert - Fill buffer completely
        for (var i = 0; i < capacity; i++)
        {
            if (i == capacity - 1)
            {
                // Last item should create batch and clear buffer
                Assert.True(packer.TryEnqueue(i, out var batch));
                Assert.NotNull(batch);
                Assert.Equal(capacity, batch.Count);
            }
            else
            {
                // Previous items should not create batches
                Assert.False(packer.TryEnqueue(i, out _));
            }
        }
    
        // Buffer should be empty after batch creation
        Assert.True(packer.IsEmpty);
    }
}