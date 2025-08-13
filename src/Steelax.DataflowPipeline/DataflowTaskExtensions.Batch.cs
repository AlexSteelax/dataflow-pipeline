using System.Buffers;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Pack data into batch
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="size"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<Batch<TInput>> Batch<TInput>(this DataflowTask<TInput> instance, int size)
    {
        return instance.Then(DataflowBatch<TInput>.Create(size));
    }
    
    /// <summary>
    /// Pack data into batch
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="size"></param>
    /// <param name="timeout"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<Batch<TInput>> Batch<TInput>(this DataflowTask<TInput> instance, int size, TimeSpan timeout)
    {
        return instance.Then(DataflowBatch<TInput>.Create(size, timeout));
    }
}