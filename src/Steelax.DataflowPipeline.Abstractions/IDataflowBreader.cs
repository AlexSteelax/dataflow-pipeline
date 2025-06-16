using System.Collections.Generic;
using System.Threading;

namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow block interface
/// </summary>
/// <typeparam name="TOutput"></typeparam>
public interface IDataflowBreader<out TOutput>
{
    /// <summary>
    /// Produce output values
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TOutput> HandleAsync(CancellationToken cancellationToken);
}