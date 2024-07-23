using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.Abstractions.Common;

internal interface IReadonlyTaskCollector
{
    Task WaitAllAsync(CancellationToken cancellationToken);
    void CopyTo(TaskCollector newCollector);
}