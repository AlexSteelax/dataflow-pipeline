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

        var tasks = new ConfiguredValueTaskAwaitable[channels.Length];
        
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                for (var i = 0; i < tasks.Length; i++)
                {
#pragma warning disable CA2012
                    tasks[i] = channels[i].WriteAsync(enumerator.Current, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2012
                }
                
                foreach (var task in tasks)
                    await task;
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