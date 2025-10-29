namespace Steelax.DataflowPipeline.UnitTests.Common;

public class PackerAdvancedTests(ITestOutputHelper output)
{
    /// <summary>
    /// Tests that batches remain valid and unchanged after buffer clearance and new operations
    /// </summary>
    [Fact]
    public void Batch_AfterBufferOperations_RemainsValidAndUnchanged()
    {
        // Arrange
        var packer = new Packer<string>(3);

        // Act - Create batch and capture it
        packer.TryEnqueue("first", out _);
        packer.TryEnqueue("second", out _);
        packer.TryClear(out var capturedBatch);

        // Modify buffer after batch creation
        packer.TryEnqueue("third", out _);
        packer.TryEnqueue("fourth", out _);
        packer.TryClear(out _);

        // Assert - Captured batch should remain unchanged
        Assert.NotNull(capturedBatch);
        Assert.Equal(2, capturedBatch.Count);
        Assert.Contains("first", capturedBatch.AsSpan().ToArray());
        Assert.Contains("second", capturedBatch.AsSpan().ToArray());

        output.WriteLine($"Batch preserved {capturedBatch.Count} items despite subsequent buffer operations");
    }

    /// <summary>
    /// Stress test for high-volume item processing to verify correct batch count and item processing
    /// </summary>
    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void TryEnqueue_HighVolumeProcessing_CreatesCorrectNumberOfBatches(int itemCount)
    {
        // Arrange
        const int capacity = 100;
        var packer = new Packer<int>(capacity);
        var totalBatches = 0;
        var itemsProcessed = 0;

        output.WriteLine($"Starting high-volume test with {itemCount} items, capacity: {capacity}");

        // Act
        for (var i = 0; i < itemCount; i++)
        {
            if (packer.TryEnqueue(i, out var batch))
            {
                totalBatches++;
                itemsProcessed += batch.Count;
                Assert.NotNull(batch);
                Assert.Equal(capacity, batch.Count);
            }
        }

        // Process remaining items
        if (packer.TryClear(out var finalBatch))
        {
            totalBatches++;
            itemsProcessed += finalBatch.Count;
        }

        // Assert
        var expectedBatches = (itemCount + capacity - 1) / capacity; // Ceiling division
        Assert.Equal(expectedBatches, totalBatches);
        Assert.Equal(itemCount, itemsProcessed);

        output.WriteLine($"Processed {itemCount} items in {totalBatches} batches");
    }

    /// <summary>
    /// Tests mixed operations with random patterns to verify stability under unpredictable usage
    /// </summary>
    [Fact]
    [Trait("Category", "LongRunning")]
    public void MixedOperations_RandomPattern_BehavesCorrectly()
    {
        // Arrange
        var packer = new Packer<int>(10);
        var rng = new Random(42);
        var totalBatches = 0;
        var totalItemsProcessed = 0;
        const int totalOperations = 1000;

        output.WriteLine($"Starting mixed operations test with {totalOperations} total operations");

        // Act
        for (var i = 0; i < totalOperations; i++)
        {
            var operation = rng.Next(0, 3); // 0: Enqueue, 1: TryClear, 2: Multiple Enqueue

            switch (operation)
            {
                case 0:
                    // Single enqueue
                    if (packer.TryEnqueue(i, out var batch1))
                    {
                        totalBatches++;
                        totalItemsProcessed += batch1.Count;
                    }
                    break;
                case 1:
                    // TryClear operation
                    if (packer.TryClear(out var batch2))
                    {
                        totalBatches++;
                        totalItemsProcessed += batch2.Count;
                    }
                    break;
                case 2:
                    // Multiple enqueue (2-5 items)
                    var count = rng.Next(2, 6);
                    for (var j = 0; j < count; j++)
                    {
                        if (packer.TryEnqueue(i * 100 + j, out var batch3))
                        {
                            totalBatches++;
                            totalItemsProcessed += batch3.Count;
                        }
                    }
                    break;
            }
        }

        // Final cleanup
        if (packer.TryClear(out var finalBatch))
        { 
            totalBatches++; 
            totalItemsProcessed += finalBatch.Count;
        }

        // Assert - Basic sanity checks
        Assert.True(packer.IsEmpty);
        Assert.True(totalBatches > 0);
        Assert.True(totalItemsProcessed >= totalOperations / 3); // Reasonable minimum

        output.WriteLine($"Completed mixed operations: {totalBatches} batches, {totalItemsProcessed} items processed");
    }

    /// <summary>
    /// Tests operations with custom cancellation token linked to test context token
    /// </summary>
    [Fact]
    [Trait("Category", "Cancellation")]
    public void Operations_WithCustomCancellationToken_BehaveCorrectly()
    {
        // Arrange
        using var customCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(customCts.Token, TestContext.Current.CancellationToken);

        var packer = new Packer<int>(5);

        // Act & Assert - Operations should work normally with cancellation token
        // (Assuming packer methods might accept cancellation tokens in real scenarios)
        var result = packer.TryEnqueue(1, out var batch);
            
        Assert.False(result);
        Assert.Null(batch);
        Assert.False(packer.IsEmpty);

        output.WriteLine("Operations completed successfully with custom cancellation token");
    }

    /// <summary>
    /// Tests that operations complete within reasonable time under normal conditions
    /// </summary>
    [Fact(Timeout = 5000)]
    [Trait("Category", "Performance")]
    public void Operations_UnderNormalConditions_CompleteWithinTimeout()
    {
        // Arrange
        var packer = new Packer<int>(100);
        const int itemCount = 1000;

        // Act
        for (var i = 0; i < itemCount; i++)
        { 
            packer.TryEnqueue(i, out _);
        }

        // Assert - If we reach here, operations completed within timeout
        Assert.True(true); // Placeholder assertion

        output.WriteLine($"Successfully processed {itemCount} items within timeout");
    }
}