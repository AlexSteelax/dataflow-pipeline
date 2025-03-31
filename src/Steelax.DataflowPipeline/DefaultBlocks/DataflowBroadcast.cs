using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowBroadcast<TValue> :
    IDataflowAction<TValue>,
    IDataflowPipe<TValue>
{
    private readonly List<ChannelWriter<TValue>> _writers = [];
    
    public ChannelReader<TValue> AttachConsumer()
    {
        var channel = Channel.CreateBounded<TValue>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
        
        _writers.Add(channel);
        
        return channel;
    }
    
    async Task IDataflowAction<TValue>.HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var tasks = new ValueTask[_writers.Count];
        
        Exception? exception = null;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
                
                for (var i = tasks.Length; i-- > 0;)
                {
                    #pragma warning disable CA2012
                    tasks[i] = _writers[i].WriteAsync(enumerator.Current, cancellationToken);
                    #pragma warning restore CA2012
                }

                for (var i = tasks.Length; i-- > 0;)
                    await tasks[i].ConfigureAwait(false);
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
        }
        
        Complete();
        
        if (exception is not null)
            throw exception;

        return;

        void Complete()
        {
            foreach (var channel in _writers)
                channel.TryComplete();

            Array.Clear(tasks);
        }
    }

    async IAsyncEnumerable<TValue> IDataflowPipe<TValue, TValue>.HandleAsync(IAsyncEnumerable<TValue> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var tasks = new ValueTask[_writers.Count];
        
        Exception? exception = null;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
                
                for (var i = tasks.Length; i-- > 0;)
                {
                    #pragma warning disable CA2012
                    tasks[i] = _writers[i].WriteAsync(enumerator.Current, cancellationToken);
                    #pragma warning restore CA2012
                }

                for (var i = tasks.Length; i-- > 0;)
                    await tasks[i].ConfigureAwait(false);
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
            
            yield return enumerator.Current;
        }
        
        Complete();
        
        if (exception is not null)
            throw exception;

        yield break;

        void Complete()
        {
            foreach (var channel in _writers)
                channel.TryComplete();

            Array.Clear(tasks);
        }
    }
}