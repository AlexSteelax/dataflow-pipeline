using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common.Delegates;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowSplitter<TValue, TIndex>(SplitIndexHandle<TValue, TIndex> splitIndexer) :
    IDataflowAction<TValue>,
    IDataflowPipe<TValue>
{
    private readonly List<FilteredWriter> _writers = [];

    private int Next(int index) => ++index >= _writers.Count ? 0 : index;
        
    
    private readonly struct FilteredWriter(Func<TIndex, bool> filter, ChannelWriter<TValue> writer)
    {
         public ValueTask WriteAsync(TIndex indexer, TValue value, CancellationToken cancellationToken = default) =>
             filter.Invoke(indexer) ? writer.WriteAsync(value, cancellationToken) : ValueTask.CompletedTask;
         
        public bool TryComplete() =>
            writer.TryComplete();
    }
    
    public ChannelReader<TValue> AttachConsumer(Func<TIndex, bool> filter)
    {
        var channel = Channel.CreateBounded<TValue>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });
        
        _writers.Add(new FilteredWriter(filter, channel));
        
        return channel;
    }
    
    async Task IDataflowAction<TValue>.HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var tasks = new ValueTask[_writers.Count];
        
        Exception? exception = null;

        var index = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
                
                for (var i = tasks.Length; i-- > 0;)
                {
                    var indexer = splitIndexer.Invoke(enumerator.Current, index);
                    
                    #pragma warning disable CA2012
                    tasks[i] = _writers[i].WriteAsync(indexer, enumerator.Current, cancellationToken);
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
            
            index = Next(index);
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
        
        var index = 0;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
                
                for (var i = tasks.Length; i-- > 0;)
                {
                    var indexer = splitIndexer.Invoke(enumerator.Current, index);
                    
                    #pragma warning disable CA2012
                    tasks[i] = _writers[i].WriteAsync(indexer, enumerator.Current, cancellationToken);
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
            
            index = Next(index);
            
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