using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Async;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    public static DataflowTask<T> Then<T>(this DataflowTask<T> instance, IDataflowPipe<T> dataflow)
    {
        return instance.Then<T>(dataflow.HandleAsync);
    }

    public static DataflowTask<TNext> Then<T, TNext>(this DataflowTask<T> instance, IDataflowPipe<T, TNext> dataflow)
    {
        return instance.Then(dataflow.HandleAsync);
    }

    public static DataflowTask EndWith<T>(this DataflowTask<T> instance, IDataflowAction<T> dataflow)
    {
        return instance.EndWith(dataflow.HandleAsync);
    }

    /// <summary>
    /// Union current dataflow task with another dataflow tasks
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="sources"></param>
    /// <param name="merger"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static DataflowTask<T> Union<T>(this DataflowTask<T> instance, DataflowTask<T>[] sources, IAsyncEnumerableMerger<T>? merger = null)
    {
        merger ??= AsyncEnumerableMerger<T>.Default;

        return new DataflowTask<T>(MergeAsync);

        IAsyncEnumerable<T> MergeAsync(CancellationToken token)
        {
            var streams = sources.Append(instance).Select(s => s.Handler.Invoke(token)).ToArray();
            return merger.MergeAsync(streams, token);
        }
    }
    
    /// <summary>
    /// Complete dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static DataflowTask End<T>(this DataflowTask<T> instance)
    {
        return instance.EndWith(new DataflowRunner<T>());
    }
}