using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.UnitTests.Mocks;

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

    public async Task HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken = default)
    {
        await foreach(var item in source.WithCancellation(cancellationToken))
            await _channel.Writer.WriteAsync(item, cancellationToken);
        
        _channel.Writer.TryComplete();
    }
}