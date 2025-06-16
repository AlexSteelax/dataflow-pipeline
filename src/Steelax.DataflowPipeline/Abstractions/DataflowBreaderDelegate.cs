namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow handler
/// </summary>
/// <typeparam name="TOutput"></typeparam>
public delegate IAsyncEnumerable<TOutput> DataflowBreaderDelegate<out TOutput>(CancellationToken cancellationToken);