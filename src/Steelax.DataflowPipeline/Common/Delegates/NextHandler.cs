namespace Steelax.DataflowPipeline.Common.Delegates;

public delegate IAsyncEnumerable<TOutput> NextHandler<in TInput, out TOutput>(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);

public delegate IAsyncEnumerable<TOutput> NextHandler<out TOutput>(CancellationToken cancellationToken);
