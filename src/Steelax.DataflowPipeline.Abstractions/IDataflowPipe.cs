using System.Collections.Generic;
using System.Threading;

namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow block interface
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
public interface IDataflowPipe<in TInput, out TOutput>
{
    /// <summary>
    /// Consume and transform input values into output ones
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TOutput> HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a dataflow block interface
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IDataflowPipe<T> : IDataflowPipe<T, T> { }