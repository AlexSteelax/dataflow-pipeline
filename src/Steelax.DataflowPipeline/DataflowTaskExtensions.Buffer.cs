using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Buffering data in background
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="capacity"></param>
    /// <param name="mode"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TInput> Buffer<TInput>(
        this DataflowTask<TInput> instance,
        int capacity,
        BufferMode mode = BufferMode.Wait)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        return instance.Then(new DataflowBuffer<TInput>(capacity, mode));
    }
}