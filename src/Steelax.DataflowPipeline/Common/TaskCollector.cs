using Steelax.DataflowPipeline.Abstractions.Common;
using Steelax.DataflowPipeline.Common.Delegates;

namespace Steelax.DataflowPipeline.Common;

internal sealed class TaskCollector: IReadonlyTaskCollector, IWriteableTaskCollector
{
    private readonly List<ActionHandler> _handlers = new();

    public void Add(ActionHandler handler)
    {
        _handlers.Add(handler);
    }

    public async Task WaitAllAsync(CancellationToken cancellationToken)
    {
        //var to = _handlers.Count;
        var tasks = _handlers.Select(handler => handler(cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        //Parallel.ForAsync(0, to, cancellationToken, async (s, c) => await _handlers[s](c));
    }

    public void CopyTo(TaskCollector newCollector)
    {
        foreach (var handler in _handlers)
        {
            newCollector.Add(handler);
        }
    }
}