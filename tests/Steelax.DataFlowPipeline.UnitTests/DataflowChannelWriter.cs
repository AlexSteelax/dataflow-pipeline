using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataFlowPipeline.UnitTests;

public class DataflowChannelWriter<TInput> :
    IDataflowAction<TInput>
{
    private readonly Channel<TInput> _channel = Channel.CreateUnbounded<TInput>(new UnboundedChannelOptions()
    {
        SingleReader = false,
        SingleWriter = false
    });

    public ValueTask<TInput[]> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken).ToArrayAsync(cancellationToken);

    public async Task HandleAsync(IAsyncEnumerable<TInput> stream, CancellationToken cancellationToken = default)
    {
        await using var enumerator = stream.GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            await _channel.Writer.WriteAsync(enumerator.Current, cancellationToken);
        }

        _channel.Writer.TryComplete();
    }
}