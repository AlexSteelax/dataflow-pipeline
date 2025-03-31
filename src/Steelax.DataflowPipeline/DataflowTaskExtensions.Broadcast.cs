using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Passing same data to each dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Broadcast<TInput>(this DataflowTask<TInput> instance, Func<DataflowTask<TInput>, DataflowTask>[] dataflows)
    {
        var broadcast = new DataflowBroadcast<TInput>();
        
        var items = dataflows
            .Select(dataflow =>
                dataflow.Invoke(broadcast.AttachConsumer().UseAsDataflowSource()))
            .ToArray();
        
        return instance.Attach(items).EndWith(broadcast);
    }
    
    /// <summary>
    /// Passing same data to each dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflows"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TInput> BroadcastContinue<TInput>(this DataflowTask<TInput> instance, Func<DataflowTask<TInput>, DataflowTask>[] dataflows)
    {
        var broadcast = new DataflowBroadcast<TInput>();
        
        var items = dataflows
            .Select(dataflow =>
                dataflow.Invoke(broadcast.AttachConsumer().UseAsDataflowSource()))
            .ToArray();
        
        return instance.Attach(items).Then(broadcast);
    }
}