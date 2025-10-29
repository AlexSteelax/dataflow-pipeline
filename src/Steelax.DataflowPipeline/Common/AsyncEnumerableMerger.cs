using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Async;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline.Common;

public sealed class AsyncEnumerableMerger<T>(FaultToleranceMode faultToleranceMode) : IAsyncEnumerableMerger<T>
{
    private AsyncEnumerableMerger() : this(FaultToleranceMode.Strict) { }
    
    public IAsyncEnumerable<T> MergeAsync(IAsyncEnumerable<T>[] sources, CancellationToken cancellationToken)
    {
        if (sources.Length == 0)
            return AsyncEnumerable.Empty<T>();

        if (sources.Length == 1 && faultToleranceMode == FaultToleranceMode.Strict)
            return sources[0];

        return MultipleMergeAsync(sources, cancellationToken);
    }

    private async IAsyncEnumerable<T> MultipleMergeAsync(IAsyncEnumerable<T>[] sources, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var merger = AsyncMergeBuilder<T>
            .UseSource(sources)
            .WithFaultStrategy(faultToleranceMode)
            .WithCancellation(cancellationToken).Build();

        await using var enumerator = merger.GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync())
        {
            yield return enumerator.Current;
        }
    }
    
    public static readonly AsyncEnumerableMerger<T> Default = new();
}