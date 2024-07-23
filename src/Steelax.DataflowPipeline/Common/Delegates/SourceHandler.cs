namespace Steelax.DataflowPipeline.Common.Delegates;

internal delegate IAsyncEnumerable<TValue> SourceHandler<out TValue>(CancellationToken cancellationToken);