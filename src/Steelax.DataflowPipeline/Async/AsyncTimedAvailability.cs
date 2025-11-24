using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using DotNext.Threading;
using Steelax.DataflowPipeline.Common;
using Timeout = System.Threading.Timeout;

namespace Steelax.DataflowPipeline.Async;

public sealed class AsyncTimedAvailability<T> : IDisposable, IAsyncDisposable
{
    private readonly TimeSpan _timeout;
    private readonly Timer _timer;
    private readonly AsyncAutoResetEvent _autoResetEvent;

    private bool _disposed;
    
    public AsyncTimedAvailability(TimeSpan timeout)
    {
        _timeout = timeout;
        _timer = new Timer(OnFire, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _autoResetEvent = new AsyncAutoResetEvent(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnFire(object? _)
    {
        try
        {
            _autoResetEvent.Set();
        }
        catch
        {
            // Expected during disposal - AutoResetEvent may be disposed
            // before all pending callbacks complete
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnFire()
    {
        try
        {
            _autoResetEvent.Set();
        }
        catch
        {
            // Expected during disposal - AutoResetEvent may be disposed
            // before all pending callbacks complete
        }
    }

    public async IAsyncEnumerable<TimedAvailability<T>> WaitTimeoutAsync(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);

        // Ставим новую задачу и запускаем таймер ожидания
        var task = MoveNext(enumerator);
        ResetOnceTimer();
        
        while (true)
        {
            try
            {
                await _autoResetEvent.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            var timestamp = DateTimeOffset.UtcNow;

            if (task.IsCompleted)
            {
                // Останавливаем таймер и сбрасываем счетчик ожиданий
                StopTimer();
                _autoResetEvent.Reset();
                
                if (task.IsCompletedSuccessfully)
                {
                    if (task.Result)
                    {
                        // Возвращаем потребителю
                        yield return TimedAvailability<T>.Available(enumerator.Current, timestamp);

                        // Ставим новую задачу и запускаем таймер ожидания
                        task = MoveNext(enumerator);
                        ResetOnceTimer();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    await DisposeEnumerator(enumerator);
                    throw task.AsTask().Exception!.GetBaseException();
                }
            }
            else
            {
                yield return TimedAvailability<T>.Timeout(timestamp);
                
                // Запускаем таймер
                ResetOnceTimer();
            }
        }
        
        await DisposeEnumerator(enumerator);
    }

    public async IAsyncEnumerable<TimedAvailability<T>> WaitPeriodicallyAsync(
        IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);

        // Ставим новую задачу и запускаем таймер ожидания
        var task = MoveNext(enumerator);
        ResetPeriodicallyTimer();
        
        while (true)
        {
            try
            {
                await _autoResetEvent.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            var timestamp = DateTimeOffset.UtcNow;

            if (task.IsCompleted)
            {
                if (task.IsCompletedSuccessfully)
                {
                    if (task.Result)
                    {
                        // Возвращаем потребителю
                        yield return TimedAvailability<T>.Available(enumerator.Current, timestamp);

                        // Ставим новую задачу
                        task = MoveNext(enumerator);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    await DisposeEnumerator(enumerator);
                    throw task.AsTask().Exception!;
                }
            }
            else
            {
                yield return TimedAvailability<T>.Timeout(timestamp);
            }
        }
        
        await DisposeEnumerator(enumerator);
    }

    private static async ValueTask DisposeEnumerator(IAsyncEnumerator<T> enumerator)
    {
        if (enumerator is IAsyncDisposable disposable)
            try
            {
                await disposable.DisposeAsync();
            }
            catch (NotSupportedException)
            {
                
            }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetOnceTimer() => _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetPeriodicallyTimer() => _timer.Change(_timeout, _timeout);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StopTimer() => _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private ValueTask<bool> MoveNext(IAsyncEnumerator<T> enumerator)
    {
#pragma warning disable CA2012 // Use ValueTask correctly - we're handling completion explicitly
        var task = enumerator.MoveNextAsync();
#pragma warning restore CA2012
        
        // Register completion callback if not already completed
        if (task.GetAwaiter() is { IsCompleted: false } awaiter)
        {
            awaiter.OnCompleted(OnFire);
        }
        else
        {
            // Immediate completion - signal availability
            OnFire();
        }

        return task;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        _timer.Dispose();
        try
        {
            _autoResetEvent.Dispose();
        }
        catch (NotSupportedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        await _timer.DisposeAsync();

        try
        {
            await _autoResetEvent.DisposeAsync();
        }
        catch (NotSupportedException)
        {
        }
    }
}