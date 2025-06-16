using Steelax.DataflowPipeline.AlgorithmBlock;

namespace Steelax.DataflowPipeline.UnitTests.DefaultBlocks.Algorithmic;

public class TestMessage
{
    public required string Key { get; init; }
    public required int Value { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public class TestChangeTracker : DataflowChangeTracker<TestMessage, string, int>
{
    private readonly Dictionary<string, TrackedValue<int>> _tracker = [];
    
    protected override ValueTask<TrackedValue<int>?> TryGetTrackedValue(string key)
    {
        return _tracker.TryGetValue(key, out var value)
            ? new ValueTask<TrackedValue<int>?>(value)
            : default;
    }

    protected override ValueTask UpdateTrackedValue(string key, TrackedValue<int> value)
    {
        _tracker[key] = value;
        
        return ValueTask.CompletedTask;
    }

    protected override TrackedValue<int> DeconstructValue(TestMessage message) => 
        new(message.Value, message.Timestamp);

    protected override string DeconstructKey(TestMessage message) => 
        message.Key;
}

public class DataflowChangeTrackerTests
{
    private readonly TestChangeTracker _tracker = new();

    [Fact]
    public async Task HandleAsync_WhenFirstMessage_ShouldReturnIt()
    {
        // Arrange
        var message = new TestMessage 
        { 
            Key = "test", 
            Value = 42, 
            Timestamp = DateTimeOffset.UtcNow 
        };
        var source = new[] { message }.ToAsyncEnumerable();

        // Act
        var result = await _tracker.HandleAsync(source, CancellationToken.None).ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(message, result[0]);
    }

    [Fact]
    public async Task HandleAsync_WhenValueChanged_ShouldReturnNewMessage()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new TestMessage { Key = "test", Value = 42, Timestamp = timestamp },
            new TestMessage { Key = "test", Value = 43, Timestamp = timestamp.AddSeconds(1) }
        }.ToAsyncEnumerable();

        // Act
        var result = await _tracker.HandleAsync(messages, CancellationToken.None).ToListAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(42, result[0].Value);
        Assert.Equal(43, result[1].Value);
    }

    [Fact]
    public async Task HandleAsync_WhenValueNotChanged_ShouldSkipMessage()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new TestMessage { Key = "test", Value = 42, Timestamp = timestamp },
            new TestMessage { Key = "test", Value = 42, Timestamp = timestamp.AddSeconds(1) }
        }.ToAsyncEnumerable();

        // Act
        var result = await _tracker.HandleAsync(messages, CancellationToken.None).ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0].Value);
    }

    [Fact]
    public async Task HandleAsync_WhenOlderMessage_ShouldSkipIt()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new TestMessage { Key = "test", Value = 42, Timestamp = timestamp },
            new TestMessage { Key = "test", Value = 43, Timestamp = timestamp.AddSeconds(-1) }
        }.ToAsyncEnumerable();

        // Act
        var result = await _tracker.HandleAsync(messages, CancellationToken.None).ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(42, result[0].Value);
    }

    [Fact]
    public async Task HandleAsync_WhenDifferentKeys_ShouldTrackSeparately()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new TestMessage { Key = "key1", Value = 42, Timestamp = timestamp },
            new TestMessage { Key = "key2", Value = 42, Timestamp = timestamp },
            new TestMessage { Key = "key1", Value = 43, Timestamp = timestamp.AddSeconds(1) },
            new TestMessage { Key = "key2", Value = 44, Timestamp = timestamp.AddSeconds(1) }
        }.ToAsyncEnumerable();

        // Act
        var result = await _tracker.HandleAsync(messages, CancellationToken.None).ToListAsync();

        // Assert
        Assert.Equal(4, result.Count);
        Assert.Equal(42, result[0].Value);
        Assert.Equal(42, result[1].Value);
        Assert.Equal(43, result[2].Value);
        Assert.Equal(44, result[3].Value);
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_ShouldStopProcessing()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var messages = new[]
        {
            new TestMessage { Key = "test", Value = 42, Timestamp = DateTimeOffset.UtcNow },
            new TestMessage { Key = "test", Value = 43, Timestamp = DateTimeOffset.UtcNow.AddSeconds(1) }
        }.ToAsyncEnumerable();

        // Act
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await _tracker.HandleAsync(messages, cts.Token).ToListAsync(CancellationToken.None));
    }
} 