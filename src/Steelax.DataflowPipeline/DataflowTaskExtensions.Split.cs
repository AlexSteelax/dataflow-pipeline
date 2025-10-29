using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Passing data to each dataflow task with precondition
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="splitIndexer"></param>
    /// <param name="filteredDataflows"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TIndex"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput, TIndex>(
        this DataflowTask<TInput> instance,
        SplitIndexHandle<TInput, TIndex> splitIndexer,
        (Func<TIndex, bool> Filter, Func<DataflowTask<TInput>, DataflowTask> Dataflow)[] filteredDataflows)
    {
        var split = new DataflowSplitter<TInput, TIndex>(splitIndexer);
        
        var items = filteredDataflows
            .Select(filteredDataflow =>
            {
                var reader = split.AttachConsumer(filteredDataflow.Filter);
                var source = DataflowTask.From([reader.ReadAllAsync]);
                return filteredDataflow.Dataflow.Invoke(source);
            })
            .ToArray();

        return instance.EndWith(split).Attach(items);
    }
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput>(
        this DataflowTask<TInput> instance,
        Func<DataflowTask<TInput>, DataflowTask>[] dataflows)
    {
        return instance.Split(SplitIndexer, dataflows.Select(FilteredDataflow).ToArray());

        static int SplitIndexer(TInput _, int index) =>
            index;

        static (Func<int, bool>, Func<DataflowTask<TInput>, DataflowTask>) FilteredDataflow(Func<DataflowTask<TInput>, DataflowTask> dataflow, int num) =>
            (index => index == num, dataflow);
    }
}