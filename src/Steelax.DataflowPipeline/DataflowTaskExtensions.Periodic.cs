using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Interrupt flow with const intervals
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="interval"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TimedAvailability<TInput>> Periodic<TInput>(this DataflowTask<TInput> instance, TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfZero(interval.Ticks, nameof(interval));

        return instance.Then(new DataflowPeriodic<TInput>(interval, false));
    }
}