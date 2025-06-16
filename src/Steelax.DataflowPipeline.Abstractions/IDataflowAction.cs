using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow block interface
/// </summary>
/// <typeparam name="TInput"></typeparam>
public interface IDataflowAction<in TInput>
{
    /// <summary>
    /// Consume and handle input values
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);
}