using Steelax.DataflowPipeline.Common.Delegates;

namespace Steelax.DataflowPipeline.Abstractions.Common;

internal interface IWriteableTaskCollector
{
    void Add(ActionHandler handler);
}