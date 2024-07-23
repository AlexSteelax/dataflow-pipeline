namespace Steelax.DataflowPipeline.Common.Delegates;

public delegate Task CompleteHandler<in TInput>(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);