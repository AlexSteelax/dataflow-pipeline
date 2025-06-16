using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline;

public class DataflowTask(Func<CancellationToken, Task> handler)
{
    private readonly List<Func<CancellationToken, Task>> _handlers = [handler];
    
    public IReadOnlyList<Func<CancellationToken, Task>> Handlers => _handlers;

    public static DataflowTask<T> From<T>(params DataflowBreaderDelegate<T>[] handlers)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handlers.Length, nameof(handlers));
        
        return handlers.Length == 1
            ? new DataflowTask<T>(handlers[0].Invoke)
            : new DataflowTask<T>(token => handlers.Select(s => s.Invoke(token)).ToArray().MergeAsync(cancellationToken: token));
    }

    public static DataflowTask<T> From<T>(params IDataflowBreader<T>[] dataflows)
    {
        return From(dataflows.Select(s => (DataflowBreaderDelegate<T>)s.HandleAsync).ToArray());
    }

    public Task InvokeAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(_handlers.Select(s => s.Invoke(cancellationToken)));
    }

    /// <summary>
    /// Attach dataflow pipelines to current dataflow pipeline
    /// </summary>
    /// <param name="dataflows"></param>
    /// <returns></returns>
    public DataflowTask Attach(params DataflowTask[] dataflows)
    {
        _handlers.AddRange(dataflows.SelectMany(s => s._handlers));
        
        foreach (var dataflow in dataflows)
            dataflow._handlers.Clear();
        
        return this;
    }
}

public class DataflowTask<T>(Func<CancellationToken, IAsyncEnumerable<T>> handler)
{
    public Func<CancellationToken, IAsyncEnumerable<T>> Handler => handler;
    
    public DataflowTask<TNext> Then<TNext>(DataflowPipeDelegate<T, TNext> nextHandler)
    {
        return new DataflowTask<TNext>(token => nextHandler.Invoke(handler.Invoke(token), token));
    }

    public DataflowTask EndWith(DataflowActionDelegate<T> nextHandler)
    {
        return new DataflowTask(token => nextHandler.Invoke(handler.Invoke(token), token));
    }
}


// public readonly struct DataflowTask()
// {
//     private readonly TaskCollector _tasks = new();
//
//     internal DataflowTask(IReadonlyTaskCollector tasks) : this()
//     {
//         tasks.CopyTo(_tasks);
//     }
//
//     /// <summary>
//     /// Get current task collector
//     /// </summary>
//     /// <returns></returns>
//     internal IReadonlyTaskCollector TaskCollector => _tasks;
//     
//     /// <summary>
//     /// Add task handlers from input task collectors into current
//     /// </summary>
//     /// <param name="taskCollectors"></param>
//     internal void MergeTaskHandlers(params IReadonlyTaskCollector[] taskCollectors)
//     {
//         foreach (var taskCollector in taskCollectors)
//             taskCollector.CopyTo(_tasks);
//     }
//     
//     /// <summary>
//     /// 
//     /// </summary>
//     /// <param name="cancellationToken"></param>
//     /// <returns></returns>
//     public Task InvokeAsync(CancellationToken cancellationToken = default)
//     {
//         return _tasks.WaitAllAsync(cancellationToken);
//     }
//     
//     /// <summary>
//     /// Create new dataflow task from input source handler
//     /// <para>[non-thread-safe]</para>
//     /// </summary>
//     /// <param name="handler"></param>
//     /// <returns></returns>
//     public static DataflowTask<TValue> From<TValue>(Func<CancellationToken, IAsyncEnumerable<TValue>> handler)
//     {
//         return DataflowTask<TValue>.From(handler.Invoke);
//     }
// }
//
// public readonly struct DataflowTask<TValue>
// {
//     private readonly TaskCollector _tasks;
//     private readonly SourceHandler<TValue> _handler;
//     
//     private DataflowTask(TaskCollector tasks, SourceHandler<TValue> handler)
//     {
//         _tasks = tasks;
//         _handler = handler;
//     }
//
//     /// <summary>
//     /// Get current task handler
//     /// </summary>
//     /// <returns></returns>
//     // ReSharper disable once ConvertToAutoPropertyWhenPossible
//     internal SourceHandler<TValue> SourceHandler => _handler;
//
//     /// <summary>
//     /// Get current task collector
//     /// </summary>
//     /// <returns></returns>
//     internal IReadonlyTaskCollector TaskCollector => _tasks;
//
//     /// <summary>
//     /// Continue with next handler
//     /// </summary>
//     /// <param name="handler"></param>
//     /// <typeparam name="TOutput"></typeparam>
//     /// <returns></returns>
//     internal DataflowTask<TOutput> Next<TOutput>(NextHandler<TValue, TOutput> handler)
//     {
//         var gHandler = _handler;
//         return new DataflowTask<TOutput>(_tasks, token => handler(gHandler(token), token));
//     }
//     
//     /// <summary>
//     /// Continue with next handler
//     /// </summary>
//     /// <param name="handler"></param>
//     /// <typeparam name="TOutput"></typeparam>
//     /// <returns></returns>
//     internal DataflowTask<TOutput> Next<TOutput>(NextHandler<TOutput> handler)
//     {
//         return new DataflowTask<TOutput>(_tasks, token => handler(token));
//     }
//
//     /// <summary>
//     /// Add task handlers from input task collectors into current
//     /// </summary>
//     /// <param name="taskCollectors"></param>
//     internal void MergeTaskHandlers(params IReadonlyTaskCollector[] taskCollectors)
//     {
//         foreach(var taskCollector in taskCollectors)
//             taskCollector.CopyTo(_tasks);
//     }
//     
//     /// <summary>
//     /// Add task handler to current task collector
//     /// </summary>
//     /// <param name="handler"></param>
//     internal void AddTaskHandler(CompleteHandler<TValue> handler)
//     {
//         var gHandler = _handler;
//         _tasks.Add(token => handler(gHandler(token), token));
//     }
//
//     /// <summary>
//     /// Create new dataflow task from input source handler
//     /// <para>[non-thread-safe]</para>
//     /// </summary>
//     /// <param name="handler"></param>
//     /// <returns></returns>
//     internal static DataflowTask<TValue> From(SourceHandler<TValue> handler)
//     {
//         return new DataflowTask<TValue>(new TaskCollector(), handler);
//     }
// }