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

        var writers = new ValueTask[channels.Length];

        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                var splitIndex = splitHandler(enumerator.Current, index) % channels.Length;

                if (!writers[splitIndex].IsCompletedSuccessfully)
                    await writers[splitIndex].ConfigureAwait(false);
                    
                writers[splitIndex] = channels[splitIndex].WriteAsync(enumerator.Current, cancellationToken);
                
                index = Next(index);
            }

            for (var i = 0; i < writers.Length; i++)
            {
                if (!writers[i].IsCompletedSuccessfully)
                    await writers[i].ConfigureAwait(false);
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