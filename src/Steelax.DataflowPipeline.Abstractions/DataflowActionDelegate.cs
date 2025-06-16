using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Steelax.DataflowPipeline.Abstractions;

/// <summary>
/// Represents a dataflow handler
/// </summary>
/// <typeparam name="TInput"></typeparam>
public delegate Task DataflowActionDelegate<in TInput>(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);