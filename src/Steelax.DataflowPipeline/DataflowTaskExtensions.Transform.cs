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
        this DataflowTask<TimedResult<TInput>> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, bool>? filter = null)
    {
        return instance.Then(new DataflowPipe<TimedResult<TInput>, TOutput>(MapTo, Filter));

        TOutput MapTo(TimedResult<TInput> input) => mapper.Invoke(input.Value);
        bool Filter(TimedResult<TInput> input) => !input.Expired && (filter?.Invoke(input.Value) ?? true);
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
    public static DataflowTask<TimedResult<TOutput>> TimedTransform<TInput, TOutput>(
        this DataflowTask<TimedResult<TInput>> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, DateTimeOffset, bool>? filter = null)
    {
        return instance.Then(new DataflowPipe<TimedResult<TInput>, TimedResult<TOutput>>(MapTo, Filter));

        TimedResult<TOutput> MapTo(TimedResult<TInput> input) => input.Expired
            ? new TimedResult<TOutput>(input.Timestamp)
            : new TimedResult<TOutput>(mapper.Invoke(input.Value), input.Timestamp);

        bool Filter(TimedResult<TInput> input) =>
            filter?.Invoke(input.Value, input.Timestamp) ?? true;
    }
}