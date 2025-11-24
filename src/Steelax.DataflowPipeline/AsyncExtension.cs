using System.Runtime.CompilerServices;
using DotNext.Collections.Generic;
using Steelax.DataflowPipeline.Async;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline;

public static class AsyncExtension
{
    public static IAsyncEnumerable<TimedAvailability<T>> WaitTimeoutAsync<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan timeout,
        bool reset = true,
        CancellationToken cancellationToken = default)
    {
        return reset
            ? new AsyncTimedAvailability<T>(timeout).WaitTimeoutAsync(source, cancellationToken)
            : new AsyncTimedAvailability<T>(timeout).WaitPeriodicallyAsync(source, cancellationToken);
    }

    public static IAsyncEnumerable<T> MergeAsync<T>(
        this IAsyncEnumerable<T>[] sources,
        FaultToleranceMode faultToleranceMode = FaultToleranceMode.Strict,
        CancellationToken cancellationToken = default)
    {
        if (sources.Length == 0)
            return AsyncEnumerable.Empty<T>();

        if (sources.Length == 1 && faultToleranceMode == FaultToleranceMode.Strict)
            return sources[0];

        return MultipleMergeAsync(sources, faultToleranceMode, cancellationToken);
    }
    
    private static async IAsyncEnumerable<T> MultipleMergeAsync<T>(
        IAsyncEnumerable<T>[] sources,
        FaultToleranceMode faultToleranceMode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
}