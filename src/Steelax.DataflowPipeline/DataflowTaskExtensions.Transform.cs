using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
        /// <summary>
    /// Transform input flow into output
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="mapper"></param>
    /// <param name="filter"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Transform<TInput, TOutput>(
        this DataflowTask<TInput> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, bool>? filter = null)
    {
        return instance.Then(new DataflowPipe<TInput, TOutput>(mapper, filter));
    }
    
    /// <summary>
    /// Transform input flow into output
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="mapper"></param>
    /// <param name="filter"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> NoTimedTransform<TInput, TOutput>(
        this DataflowTask<TimedAvailability<TInput>> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, bool>? filter = null)
    {
        return instance.Then(new DataflowPipe<TimedAvailability<TInput>, TOutput>(MapTo, Filter));

        TOutput MapTo(TimedAvailability<TInput> input) => mapper.Invoke(input.Value);
        bool Filter(TimedAvailability<TInput> input) => !input.IsAvailable && (filter?.Invoke(input.Value) ?? true);
    }
    
    /// <summary>
    /// Transform input flow into output
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="mapper"></param>
    /// <param name="filter"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TimedAvailability<TOutput>> TimedTransform<TInput, TOutput>(
        this DataflowTask<TimedAvailability<TInput>> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, DateTimeOffset, bool>? filter = null)
    {
        return instance.Then(new DataflowPipe<TimedAvailability<TInput>, TimedAvailability<TOutput>>(MapTo, Filter));

        TimedAvailability<TOutput> MapTo(TimedAvailability<TInput> input) => input.IsAvailable
            ? TimedAvailability<TOutput>.Timeout(input.Timestamp)
            : TimedAvailability<TOutput>.Available(mapper.Invoke(input.Value), input.Timestamp);

        bool Filter(TimedAvailability<TInput> input) =>
            filter?.Invoke(input.Value, input.Timestamp) ?? true;
    }
}