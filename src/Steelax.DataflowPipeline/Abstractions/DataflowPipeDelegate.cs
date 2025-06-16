namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow handler
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
public delegate IAsyncEnumerable<TOutput> DataflowPipeDelegate<in TInput, out TOutput>(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);