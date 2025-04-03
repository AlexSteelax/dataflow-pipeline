using Steelax.DataflowPipeline.Abstractions.Common;
using Steelax.DataflowPipeline.Common;
using Steelax.DataflowPipeline.Common.Delegates;

namespace Steelax.DataflowPipeline;

public readonly struct DataflowTask()
{
    private readonly TaskCollector _tasks = new();

    internal DataflowTask(IReadonlyTaskCollector tasks) : this()
    {
        tasks.CopyTo(_tasks);
    }

    /// <summary>
    /// Get current task collector
    /// </summary>
    /// <returns></returns>
    internal IReadonlyTaskCollector TaskCollector => _tasks;
    
    /// <summary>
    /// Add task handlers from input task collectors into current
    /// </summary>
    /// <param name="taskCollectors"></param>
    internal void MergeTaskHandlers(params IReadonlyTaskCollector[] taskCollectors)
    {
        foreach (var taskCollector in taskCollectors)
            taskCollector.CopyTo(_tasks);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task InvokeAsync(CancellationToken cancellationToken = default)
    {
        return _tasks.WaitAllAsync(cancellationToken);
    }
    
    /// <summary>
    /// Create new dataflow task from input source handler
    /// <para>[non-thread-safe]</para>
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    public static DataflowTask<TValue> From<TValue>(SourceHandler<TValue> handler)
    {
        return DataflowTask<TValue>.From(handler);
    }
}

public readonly struct DataflowTask<TValue>
{
    private readonly TaskCollector _tasks;
    private readonly SourceHandler<TValue> _handler;
    
    private DataflowTask(TaskCollector tasks, SourceHandler<TValue> handler)
    {
        _tasks = tasks;
        _handler = handler;
    }

    /// <summary>
    /// Get current task handler
    /// </summary>
    /// <returns></returns>
    // ReSharper disable once ConvertToAutoPropertyWhenPossible
    internal SourceHandler<TValue> SourceHandler => _handler;

    /// <summary>
    /// Get current task collector
    /// </summary>
    /// <returns></returns>
    internal IReadonlyTaskCollector TaskCollector => _tasks;

    /// <summary>
    /// Continue with next handler
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    internal DataflowTask<TOutput> Next<TOutput>(NextHandler<TValue, TOutput> handler)
    {
        var gHandler = _handler;
        return new DataflowTask<TOutput>(_tasks, token => handler(gHandler(token), token));
    }
    
    /// <summary>
    /// Continue with next handler
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    internal DataflowTask<TOutput> Next<TOutput>(NextHandler<TOutput> handler)
    {
        return new DataflowTask<TOutput>(_tasks, token => handler(token));
    }

    /// <summary>
    /// Add task handlers from input task collectors into current
    /// </summary>
    /// <param name="taskCollectors"></param>
    internal void MergeTaskHandlers(params IReadonlyTaskCollector[] taskCollectors)
    {
        foreach(var taskCollector in taskCollectors)
            taskCollector.CopyTo(_tasks);
    }
    
    /// <summary>
    /// Add task handler to current task collector
    /// </summary>
    /// <param name="handler"></param>
    internal void AddTaskHandler(CompleteHandler<TValue> handler)
    {
        var gHandler = _handler;
        _tasks.Add(token => handler(gHandler(token), token));
    }

    /// <summary>
    /// Create new dataflow task from input source handler
    /// <para>[non-thread-safe]</para>
    /// </summary>
    /// <param name="handler"></param>
    /// <returns></returns>
    internal static DataflowTask<TValue> From(SourceHandler<TValue> handler)
    {
        return new DataflowTask<TValue>(new TaskCollector(), handler);
    }
}