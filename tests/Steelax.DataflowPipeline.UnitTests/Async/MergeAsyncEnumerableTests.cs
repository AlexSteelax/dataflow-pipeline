using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Async;

namespace Steelax.DataflowPipeline.UnitTests.Async;

[SuppressMessage("Performance", "CA1861")]
public class AsyncMergeableTests
{
    #region Builder Tests

    [Fact]
    public void Builder_WithNoSources_ThrowsException()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AsyncMergeBuilder<int>.UseSource().Build());

        Assert.Equal("At least one source is required", exception.Message);
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public async Task MergeAsync_EmptySources_ReturnsEmptySequence()
    {
        // Arrange
        var source1 = AsyncEnumerable.Empty<int>();
        var source2 = AsyncEnumerable.Empty<int>();
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task MergeAsync_SingleSource_ReturnsAllElements()
    {
        // Arrange
        var source = AsyncEnumerable.Range(1, 3);
        await using var merge = AsyncMergeBuilder<int>.UseSource(source).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new[] { 1, 2, 3 }, results);
    }

    [Fact]
    public async Task MergeAsync_MultipleSources_InterleavesElements()
    {
        // Arrange
        var source1 = new[] { 1, 4, 7 }.ToAsyncEnumerable();
        var source2 = new[] { 2, 5, 8 }.ToAsyncEnumerable();
        var source3 = new[] { 3, 6, 9 }.ToAsyncEnumerable();
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - should interleave in round-robin fashion
        Assert.Equal(9, results.Count);
        // Order should be: 1, 2, 3, 4, 5, 6, 7, 8, 9
        Assert.Equal(1, results[0]);
        Assert.Equal(2, results[1]);
        Assert.Equal(3, results[2]);
    }

    #endregion

    #region Fault Tolerance Tests

    [Fact]
    public async Task MergeAsync_StrictMode_ThrowsOnFirstException()
    {
        // Arrange
        var source1 = AsyncEnumerable.Range(1, 3);
        var source2 = FailingSequence<int>(new InvalidOperationException("Test failure"));
        var source3 = AsyncEnumerable.Range(7, 2);
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).WithFaultStrategy(FaultToleranceMode.Strict).Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in merge)
            {
                // Process some items before failure
            }
        });

        Assert.Equal("Test failure", exception.Message);
    }

    [Fact]
    public async Task MergeAsync_TolerantToCancellationMode_IgnoresCancellationButThrowsOtherExceptions()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var source1 = AsyncEnumerable.Range(1, 3);
        var source2 = CancelledSequence<int>(cts.Token);
        var source3 = FailingSequence<int>(new InvalidOperationException("Real failure"));
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).WithFaultStrategy(FaultToleranceMode.TolerantToCancellation).Build();

        // Act & Assert - should throw for non-cancellation exceptions
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in merge)
            {
                // Process items
            }
        });

        Assert.Equal("Real failure", exception.Message);
    }

    [Fact]
    public async Task MergeAsync_ResilientMode_ContinuesDespiteExceptions()
    {
        // Arrange
        var source1 = AsyncEnumerable.Range(1, 2);
        var source2 = FailingSequence<int>(new InvalidOperationException("Failure 1"));
        var source3 = FailingSequence<int>(new ArgumentException("Failure 2"));
        var source4 = AsyncEnumerable.Range(10, 2);
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3, source4).WithFaultStrategy(FaultToleranceMode.Resilient).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - should get elements from working sources only
        Assert.Equal(4, results.Count); // 2 from source1 + 2 from source4
        Assert.All(results, x => Assert.True(x == 1 || x == 2 || x == 10 || x == 11));
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task MergeAsync_GlobalCancellation_StopsImmediately()
    {
        // Arrange
        using var globalCts = new CancellationTokenSource();
        var source1 = InfiniteSequence(1);
        var source2 = InfiniteSequence(2);
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2).WithCancellation(globalCts.Token).Build();

        // Act & Assert
        globalCts.CancelAfter(100);
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in merge)
            {
                // Should be canceled quickly
            }
        });
    }

    [Fact]
    public async Task MergeAsync_LocalCancellation_StopsCurrentEnumeratorOnly()
    {
        // Arrange
        using var localCts = new CancellationTokenSource();
        var source1 = AsyncEnumerable.Range(1, 3);
        var source2 = AsyncEnumerable.Range(4, 3);
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2).WithFaultStrategy(FaultToleranceMode.TolerantToCancellation).Build();

        // Act
        var results = new List<int>();
        localCts.CancelAfter(50);
        
        await foreach (var item in merge.WithCancellation(localCts.Token))
        {
            results.Add(item);
            if (results.Count >= 2) break; // Ensure we process some items
        }

        // Assert - should get some items before cancellation
        Assert.NotEmpty(results);
    }

    #endregion

    #region Enumerator State Tests

    [Fact]
    public async Task Enumerator_AfterException_ThrowsSameExceptionOnSubsequentCalls()
    {
        // Arrange
        var source = FailingSequence<int>(new InvalidOperationException("Permanent failure"));
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source).WithFaultStrategy(FaultToleranceMode.Strict).Build();

        var enumerator = merge.GetAsyncEnumerator(TestContext.Current.CancellationToken);
        
        // Act & Assert - First call should throw
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await enumerator.MoveNextAsync());

        // Subsequent calls should throw the same exception
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await enumerator.MoveNextAsync());
        Assert.Equal("Permanent failure", exception.Message);
    }

    [Fact]
    public async Task Enumerator_CurrentBeforeMoveNext_ThrowsException()
    {
        // Arrange
        await using var merge = AsyncMergeBuilder<int>.UseSource(AsyncEnumerable.Range(1, 1)).Build();

        var enumerator = merge.GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => enumerator.Current);
    }

    [Fact]
    public async Task Enumerator_IndexProperty_ReturnsCorrectSourceIndex()
    {
        // Arrange
        var source1 = new[] { 100 }.ToAsyncEnumerable(); // Index 0
        var source2 = new[] { 200 }.ToAsyncEnumerable(); // Index 1
        var source3 = new[] { 300 }.ToAsyncEnumerable(); // Index 2
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).Build();
        await using var enumerator = merge.GetAsyncEnumerator(TestContext.Current.CancellationToken);

        // Act
        var indices = new List<int>();
        while (await enumerator.MoveNextAsync())
        {
            indices.Add(enumerator.Index);
        }

        // Assert - should track source indices correctly
        Assert.Contains(0, indices);
        Assert.Contains(1, indices);
        Assert.Contains(2, indices);
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public async Task MergeAsync_DisposeAsync_CleansUpAllResources()
    {
        // Arrange
        var source1 = new TrackingAsyncEnumerable<int>(new[] { 1, 2, 3 });
        var source2 = new TrackingAsyncEnumerable<int>(new[] { 4, 5, 6 });
        
        var merge = AsyncMergeBuilder<int>.UseSource(source1, source2).Build();

        // Act - Partial enumeration then disposal
        var count = 0;
        await foreach (var _ in merge)
        {
            count++;
            if (count >= 2) break;
        }
        
        await merge.DisposeAsync();

        // Assert - all enumerators should be disposed
        Assert.True(source1.Disposed);
        Assert.True(source2.Disposed);
    }

    [Fact]
    public async Task MergeAsync_MultipleDisposeAsync_IsIdempotent()
    {
        // Arrange
        var merge = AsyncMergeBuilder<int>.UseSource(AsyncEnumerable.Range(1, 3)).Build();

        // Act & Assert - Should not throw on multiple disposal
        await merge.DisposeAsync();
        await merge.DisposeAsync();
        await merge.DisposeAsync();
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task MergeAsync_SourcesCompleteAtDifferentTimes_HandlesGracefully()
    {
        // Arrange
        var source1 = new[] { 1, 2, 3, 4, 5 }.ToAsyncEnumerable(); // Long sequence
        var source2 = new[] { 10 }.ToAsyncEnumerable(); // Short sequence
        var source3 = new[] { 20, 21 }.ToAsyncEnumerable(); // Medium sequence
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - should get all elements despite different lengths
        Assert.Equal(8, results.Count); // 5 + 1 + 2
        Assert.Contains(1, results);
        Assert.Contains(10, results);
        Assert.Contains(21, results);
    }

    [Fact]
    public async Task MergeAsync_AllSourcesFailInResilientMode_ReturnsEmptySequence()
    {
        // Arrange
        var source1 = FailingSequence<int>(new Exception("Fail 1"));
        var source2 = FailingSequence<int>(new Exception("Fail 2"));
        var source3 = FailingSequence<int>(new Exception("Fail 3"));
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).WithFaultStrategy(FaultToleranceMode.Resilient).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public async Task MergeAsync_ManySources_ScalesEfficiently()
    {
        // Arrange
        const int sourceCount = 20;
        const int itemsPerSource = 5;
        
        var sources = new IAsyncEnumerable<int>[sourceCount];
        for (var i = 0; i < sourceCount; i++)
        {
            sources[i] = AsyncEnumerable.Range(i * 100, itemsPerSource);
        }
        
        await using var merge = AsyncMergeBuilder<int>.UseSource(sources).Build();

        // Act
        var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(sourceCount * itemsPerSource, results.Count);
    }

    [Fact]
    public async Task MergeAsync_StressTest_ConsistentBehavior()
    {
        // Arrange
        const int iterations = 5;
        var resultsCounts = new List<int>();

        for (var i = 0; i < iterations; i++)
        {
            var source1 = CreateDelayedSequence(new[] { 1, 2 }, 10);
            var source2 = CreateDelayedSequence(new[] { 3, 4 }, 5);
            var source3 = CreateDelayedSequence(new[] { 5, 6 }, 1);
            
            var merge = AsyncMergeBuilder<int>.UseSource(source1, source2, source3).Build();

            // Act
            var results = await merge.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            resultsCounts.Add(results.Count);
            await merge.DisposeAsync();
        }

        // Assert - all iterations should yield same number of elements
        const int expectedCount = 6;
        Assert.All(resultsCounts, count => Assert.Equal(expectedCount, count));
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<T> FailingSequence<T>(Exception exception)
    {
        await Task.Yield();
        throw exception;
        // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<T> CancelledSequence<T>([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        yield return default!;
    }

    private static async IAsyncEnumerable<T> InfiniteSequence<T>(T value)
    {
        while (true)
        {
            await Task.Delay(10);
            yield return value;
        }
        // ReSharper disable once IteratorNeverReturns
    }

    private static async IAsyncEnumerable<T> CreateDelayedSequence<T>(IEnumerable<T> items, int delayMs)
    {
        foreach (var item in items)
        {
            await Task.Delay(delayMs);
            yield return item;
        }
    }

    #endregion
}

#region Test Utilities

internal class TrackingAsyncEnumerable<T>(IEnumerable<T> items) : IAsyncEnumerable<T>
{
    public bool Disposed { get; private set; }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TrackingAsyncEnumerator(items.GetEnumerator(), this);
    }

    private class TrackingAsyncEnumerator(IEnumerator<T> enumerator, TrackingAsyncEnumerable<T> parent)
        : IAsyncEnumerator<T>
    {
        public T Current => enumerator.Current;

        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(enumerator.MoveNext());
        }

        public ValueTask DisposeAsync()
        {
            parent.Disposed = true;
            enumerator.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

#endregion