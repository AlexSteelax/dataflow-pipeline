using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Abstractions.Common;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.Common.Delegates;
using Steelax.DataflowPipeline.DefaultBlocks;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    private static IReadonlyTaskCollector[] GetTaskCollectors(this IEnumerable<DataflowTask> dataflows) =>
        dataflows.Select(s => s.TaskCollector).ToArray();
    
    private static IReadonlyTaskCollector[] GetTaskCollectors<TValue>(this IEnumerable<DataflowTask<TValue>> dataflows) =>
        dataflows.Select(s => s.TaskCollector).ToArray();
    
    /// <summary>
    /// Attach dataflow pipelines to current dataflow pipeline
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask Attach(this DataflowTask instance, params DataflowTask[] dataflows)
    {
        instance.MergeTaskHandlers(dataflows.GetTaskCollectors());
        return instance;
    }
    
    /// <summary>
    /// Attach dataflow pipelines to current dataflow pipeline
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<T> Attach<T>(this DataflowTask<T> instance, params DataflowTask[] dataflows)
    {
        instance.MergeTaskHandlers(dataflows.GetTaskCollectors());
        return instance;
    }
    
    /// <summary>
    /// Push through the next dataflow block
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflow"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> Then<TInput, TOutput>(this DataflowTask<TInput> instance, IDataflowPipe<TInput, TOutput> dataflow)
    {
        return instance.Next(dataflow.HandleAsync);
    }
    
    /// <summary>
    /// Complete dataflow task with dataflow block
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflow"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask EndWith<TInput>(this DataflowTask<TInput> instance, IDataflowAction<TInput> dataflow)
    {
        instance.AddTaskHandler(dataflow.HandleAsync);
        return new DataflowTask(instance.TaskCollector);
    }
    
    /// <summary>
    /// Complete dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask End<TInput>(this DataflowTask<TInput> instance)
    {
        return instance.EndWith(new DataflowRunner<TInput>());
    }
    
    /// <summary>
    /// Union current dataflow task with another dataflow tasks
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TInput> Union<TInput>(this DataflowTask<TInput> instance, params DataflowTask<TInput>[] dataflows)
    {
        instance.MergeTaskHandlers(dataflows.GetTaskCollectors());

        return instance.Next((source, token) =>
        {
            var sources = dataflows
                .Select(s => s.SourceHandler(token))
                .Append(source)
                .ToArray();

            return AsyncEnumerable.MergeAsync(sources, cancellationToken: token);
        });
    }
}