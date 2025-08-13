using System.Buffers;
using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowBatch<T> :
    IDataflowPipe<T, Batch<T>>
{
    private readonly int _size;
    private readonly TimeSpan _interval;
    
    private DataflowBatch(int size, TimeSpan interval)
    {
        _size = size;
        _interval = interval;
    }

    public static IDataflowPipe<T, Batch<T>> Create(int size, TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfZero(interval.Ticks, nameof(interval));

        return new DataflowBatch<T>(size, interval);
    }
    
    public static IDataflowPipe<T, Batch<T>> Create(int size)
    {
        return new DataflowBatch<T>(size, Timeout.InfiniteTimeSpan);
    }
    
    public IAsyncEnumerable<Batch<T>> HandleAsync(IAsyncEnumerable<T> source, CancellationToken cancellationToken) => _interval == Timeout.InfiniteTimeSpan
        ? HandleAsync<T, T>(
            source,
            _size,
            s => s,
            s => false,
            cancellationToken)
        : HandleAsync<TimedResult<T>, T>(
            source.WaitTimeoutAsync(_interval, true, cancellationToken),
            _size,
            s => s.Value,
            s => s.Expired,
            cancellationToken);

    private static async IAsyncEnumerable<Batch<TOutput>> HandleAsync<TInput, TOutput>(
        IAsyncEnumerable<TInput> source,
        int size,
        Func<TInput, TOutput> mapper,
        Func<TInput, bool> completer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var packer = new Packer<TOutput>(size);
        
        Batch<TOutput> buffer;
        
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        Exception? exception = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                break;
            }
            catch (Exception ex)
            {
                exception = ex;
                break;
            }

            var result = enumerator.Current;

            if (completer.Invoke(result))
            {
                _ = packer.TryClearAndGet(out buffer);
                yield return buffer;
            }
            else if (packer.TryAddAndGet(mapper.Invoke(result), out buffer))
            {
                yield return buffer;
            }
        }
        
        if (packer.TryClearAndGet(out buffer))
            yield return buffer;
        
        if (exception is not null)
            throw exception;
    }
}