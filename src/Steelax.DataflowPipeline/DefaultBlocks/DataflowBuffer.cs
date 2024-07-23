using Steelax.DataflowPipeline.Abstractions;
using System.Threading.Channels;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowBuffer<TValue>(Channel<TValue> channel) :
    IDataflowBackgroundTransform<TValue, TValue>
{
    public IAsyncEnumerable<TValue> HandleAsync(CancellationToken cancellationToken)
    {
        return channel.Reader.ReadAllAsync(cancellationToken);
    }

    public async Task HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var value in source.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                await channel.Writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            channel.Writer.Complete();
        }
    }
}