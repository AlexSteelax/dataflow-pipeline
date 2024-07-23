using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common.Delegates;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowSplitter<TValue>(SplitHandler<TValue> splitHandler,  params ChannelWriter<TValue>[] channels) :
    IDataflowAction<TValue>
{
    private int Next(int index) => ++index >= channels.Length ? 0 : index;
    
    public async Task HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var index = 0;

        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var splitIndex = splitHandler(enumerator.Current, index) % channels.Length;
                
                await channels[splitIndex].WriteAsync(enumerator.Current, cancellationToken).ConfigureAwait(false);
                
                index = Next(index);
            }
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.TryComplete();
            }
        }
    }
}