using System.Collections.Concurrent;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.UnitTests.Mocks;

public class DataflowChannelWriter<TInput> :
    IDataflowAction<TInput>
{
    private readonly ConcurrentQueue<TInput> _queue = [];
    
    public IReadOnlyList<TInput> ReadAll(CancellationToken cancellationToken = default) =>
        _queue.ToList();

    public async Task HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken = default)
    {
        await foreach(var item in source.WithCancellation(cancellationToken))
            _queue.Enqueue(item);
    }
}