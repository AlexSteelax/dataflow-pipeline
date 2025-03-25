using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowBroadcast<TValue>(params ChannelWriter<TValue>[] channels) :
    IDataflowAction<TValue>
{
    public async Task HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var tasks = new ValueTask[channels.Length];
        
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                for (var i = tasks.Length; i-- > 0;)
                {
                    #pragma warning disable CA2012
                    tasks[i] = channels[i].WriteAsync(enumerator.Current, cancellationToken);
                    #pragma warning restore CA2012
                }

                for (var i = tasks.Length; i-- > 0;)
                    await tasks[i].ConfigureAwait(false);
            }
        }
        finally
        {
            foreach (var channel in channels)
            {
                channel.TryComplete();
            }
            Array.Clear(tasks);
        }
    }
}