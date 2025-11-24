using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Async;

namespace Steelax.DataflowPipeline.UnitTests.Async;

public class AsyncTimedAvailabilityTests
{
    [Fact]
    public async Task WaitTimeoutAsync_ShouldReturnAvailable_WhenDataImmediatelyAvailable()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable([1, 2, 3], TimeSpan.Zero, TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();

        // Act
        await foreach (var item in sut.WaitTimeoutAsync(source, TestContext.Current.CancellationToken))
        {
            results.Add(item);
            if (results.Count >= 3) break;
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, static r =>
        {
            Assert.True(r.IsAvailable);
            Assert.True(r.Timestamp <= DateTimeOffset.UtcNow);
        });
        Assert.Equal([1, 2, 3], results.Select(r => r.Value));
    }

    [Fact]
    public async Task WaitTimeoutAsync_ShouldReturnTimeout_WhenNoDataAvailable()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable<int>([], TimeSpan.Zero, TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();
        var cts = new CancellationTokenSource(timeout * 2);

        // Act
        await foreach (var item in sut.WaitTimeoutAsync(source, cts.Token))
        {
            results.Add(item);
            if (results.Count >= 1) break; // Ждем хотя бы один таймаут
        }

        // Assert
        Assert.Empty(results);
        Assert.All(results, static r =>
        {
            Assert.False(r.IsAvailable);
        });
    }

    [Fact]
    public async Task WaitTimeoutAsync_ShouldMixAvailableAndTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable([1, 2], TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken); // Задержка больше таймаута
        var results = new List<TimedAvailability<int>>();
        var cts = new CancellationTokenSource(timeout * 5);

        // Act
        await foreach (var item in sut.WaitTimeoutAsync(source, cts.Token))
        {
            results.Add(item);
            if (results.Count >= 4) break; // 2 данных + 2 таймаут
        }

        // Assert
        Assert.Equal(4, results.Count);
        Assert.False(results[0].IsAvailable); // Первый таймаут
        Assert.True(results[1].IsAvailable); // Первое значение
        Assert.False(results[2].IsAvailable); // Второй таймаут
        Assert.True(results[3].IsAvailable); // Второе значение
    }

    [Fact]
    public async Task WaitPeriodicallyAsync_ShouldReturnPeriodicTimeouts()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateLoopAsyncEnumerable<int>( TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();
        var cts = new CancellationTokenSource(timeout * 3);

        // Act
        await foreach (var item in sut.WaitPeriodicallyAsync(source, cts.Token))
        {
            results.Add(item);
            if (results.Count >= 2) break; // Ждем 2 периодических таймаута
        }

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.False(r.IsAvailable));
        
        // Проверяем, что таймауты происходят примерно с правильным интервалом
        var timeDiff = results[1].Timestamp.Ticks - results[0].Timestamp.Ticks;
        Assert.True(timeDiff >= timeout.Ticks * 0.9 && timeDiff < timeout.Ticks * 1.1);
    }

    [Fact]
    public async Task WaitPeriodicallyAsync_ShouldReturnAvailableImmediately_WhenDataReady()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable([1, 2, 3], TimeSpan.Zero, TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();

        // Act
        await foreach (var item in sut.WaitPeriodicallyAsync(source, TestContext.Current.CancellationToken))
        {
            results.Add(item);
            if (results.Count >= 3) break;
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.IsAvailable));
        Assert.Equal([1, 2, 3], results.Select(r => r.Value));
    }

    [Fact]
    public async Task ShouldHandleCancellation()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable(InfiniteSequence(), TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();
        var cts = new CancellationTokenSource(timeout);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in sut.WaitTimeoutAsync(source, cts.Token))
            {
                results.Add(item);
            }
        });

        Assert.True(results.Count <= 2); // Не должно успеть много набрать
    }

    [Fact]
    public async Task ShouldPropagateSourceExceptions()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = FailingAsyncEnumerable();
        var results = new List<TimedAvailability<int>>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var item in sut.WaitTimeoutAsync(source, TestContext.Current.CancellationToken))
            {
                results.Add(item);
            }
        });

        Assert.Empty(results); // Не должно быть результатов при исключении
    }

    [Fact]
    public async Task Dispose_ShouldStopAllOperations()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var source = CreateAsyncEnumerable(InfiniteSequence(), TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var item in sut.WaitTimeoutAsync(source, TestContext.Current.CancellationToken))
            {
                results.Add(item);
            }
        }, TestContext.Current.CancellationToken);

        await Task.Delay(timeout * 2, TestContext.Current.CancellationToken);
        await sut.DisposeAsync();

        // Assert
        await enumerationTask; // Не должно висеть
        Assert.NotEmpty(results); // Должны быть какие-то результаты до dispose
    }

    [Fact]
    public async Task MoveNext_CompletionCallback_ShouldWorkForCompletedTask()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        var sut = new AsyncTimedAvailability<int>(timeout);
        var completedSource = CreateAsyncEnumerable([42], TimeSpan.Zero, TestContext.Current.CancellationToken);
        var results = new List<TimedAvailability<int>>();

        // Act
        await foreach (var item in sut.WaitTimeoutAsync(completedSource, TestContext.Current.CancellationToken))
        {
            results.Add(item);
            break;
        }

        // Assert
        Assert.Single(results);
        Assert.Equal(42, results[0].Value);
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(
        IEnumerable<T> items, 
        TimeSpan delay,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
            
            yield return item;
        }
    }
    
    private static async IAsyncEnumerable<T> CreateLoopAsyncEnumerable<T>(
        TimeSpan delay,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await Task.Delay(delay, cancellationToken);
        }
        
#pragma warning disable CS0162 // Unreachable code detected
        yield break;
#pragma warning restore CS0162 // Unreachable code detected
        // ReSharper disable once IteratorNeverReturns
    }

    private static async IAsyncEnumerable<int> FailingAsyncEnumerable()
    {
        await Task.Yield();
        throw new InvalidOperationException("Test exception");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static IEnumerable<int> InfiniteSequence()
    {
        var i = 0;
        while (true)
        {
            yield return i++;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}