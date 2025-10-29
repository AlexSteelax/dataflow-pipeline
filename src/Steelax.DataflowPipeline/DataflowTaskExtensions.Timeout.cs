using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Interrupt flow with timeout
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="timeout"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TimedAvailability<TInput>> Timeout<TInput>(this DataflowTask<TInput> instance, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfZero(timeout.Ticks, nameof(timeout));

        return instance.Then(new DataflowPeriodic<TInput>(timeout, true));
    }
}