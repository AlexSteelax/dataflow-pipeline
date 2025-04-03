namespace Steelax.DataflowPipeline.Common.Delegates;

public delegate IAsyncEnumerable<TValue> SourceHandler<out TValue>(CancellationToken cancellationToken);