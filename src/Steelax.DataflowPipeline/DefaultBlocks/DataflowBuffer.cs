using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowBuffer<TValue>(int capacity, BufferMode mode) :
    IDataflowPipe<TValue>
{
    private readonly Channel<TValue> _channel = Channel.CreateBounded<TValue>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = mode switch
        {
            BufferMode.Wait => BoundedChannelFullMode.Wait,
            BufferMode.DropNewest => BoundedChannelFullMode.DropNewest,
            BufferMode.DropOldest => BoundedChannelFullMode.DropOldest,
            BufferMode.DropWrite => BoundedChannelFullMode.DropWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        }
    });

    private Task? _consumeTask;
    
    public async IAsyncEnumerable<TValue> HandleAsync(IAsyncEnumerable<TValue> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_consumeTask is not null)
            throw new InvalidOperationException("Can't continue operation more than once.");
        
        _consumeTask = Task.Factory.StartNew(() => ConsumeAsync(source,_channel, cancellationToken), TaskCreationOptions.LongRunning).Unwrap();

        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return item;

        await _consumeTask;
        
        _consumeTask = null;
    }

    private static async Task ConsumeAsync(IAsyncEnumerable<TValue> source, ChannelWriter<TValue> writer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var result in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(result, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            writer.Complete();
        }
        finally
        {
            writer.Complete();
        }
    }
}