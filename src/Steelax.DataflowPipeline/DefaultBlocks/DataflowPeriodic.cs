using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowPeriodic<TValue>(TimeSpan period, bool reset)
    : IDataflowPipe<TValue, TimedResult<TValue>>
{
    public IAsyncEnumerable<TimedResult<TValue>> HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        return source.WaitTimeoutAsync(period, reset, cancellationToken);
    }
}