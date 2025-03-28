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
    /// Push through the next dataflow block
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="dataflow"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> Then<TInput, TOutput>(this DataflowTask<TInput> instance, IDataflowTransform<TInput, TOutput> dataflow)
    {
        return instance.Next(dataflow.HandleAsync);
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
    public static DataflowTask<TOutput> Then<TInput, TOutput>(this DataflowTask<TInput> instance, IDataflowBackgroundTransform<TInput, TOutput> dataflow)
    {
        instance.AddTaskHandler(dataflow.HandleAsync);
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
    
    /// <summary>
    /// Passing same data to each dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="configurators"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Broadcast<TInput>(this DataflowTask<TInput> instance, params Func<ConfiguredDataflowTask<TInput>, DataflowTask>[] configurators)
    {
        var items = configurators
            .Select(handler =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();
        
        var writers = items.Select(s => s.Writer).ToArray();
        var dataflows = items.Select(s => s.Dataflow).ToArray();
       
        var broadcast = new DataflowBroadcast<TInput>(writers);

        return instance.EndWith(broadcast).Attach(dataflows);
    }
    
    /// <summary>
    /// Passing same data to each dataflow task
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="configurators"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Broadcast<TInput, TOutput>(this DataflowTask<TInput> instance, params Func<ConfiguredDataflowTask<TInput>, DataflowTask<TOutput>>[] configurators)
    {
        var items = configurators
            .Select(handler =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();

        var writers = items.Select(s => s.Writer).ToArray();
        
        var broadcast = new DataflowBroadcast<TInput>(writers);

        instance.AddTaskHandler(broadcast.HandleAsync);
        return instance.Next(token =>
        {
            var sources = items
                .Select(s => s.Dataflow.SourceHandler(token))
                .ToArray();

            return AsyncEnumerable.MergeAsync(sources, cancellationToken: token);
        });
    }
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="splitHandler"></param>
    /// <param name="count"></param>
    /// <param name="handler"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput>(this DataflowTask<TInput> instance, SplitHandler<TInput> splitHandler, int count, Func<ConfiguredDataflowTask<TInput>, DataflowTask> handler)
    {
        var items = Enumerable
            .Range(0, count)
            .Select(s =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();
        
        var writers = items.Select(s => s.Writer).ToArray();
        var dataflows = items.Select(s => s.Dataflow).ToArray();
       
        var splitter = new DataflowSplitter<TInput>(splitHandler, writers);

        return instance.EndWith(splitter).Attach(dataflows);
    }
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="splitHandler"></param>
    /// <param name="handlers"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput>(this DataflowTask<TInput> instance, SplitHandler<TInput> splitHandler, params Func<ConfiguredDataflowTask<TInput>, DataflowTask>[] handlers)
    {
        var items = handlers
            .Select(handler =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();
        
        var writers = items.Select(s => s.Writer).ToArray();
        var dataflows = items.Select(s => s.Dataflow).ToArray();
       
        var splitter = new DataflowSplitter<TInput>(splitHandler, writers);

        return instance.EndWith(splitter).Attach(dataflows);
    }

    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="count"></param>
    /// <param name="handler"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput>(this DataflowTask<TInput> instance, int count, Func<ConfiguredDataflowTask<TInput>, DataflowTask> handler)
        => instance.Split((_, i) => i, count, handler);
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="handlers"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask Split<TInput>(this DataflowTask<TInput> instance, params Func<ConfiguredDataflowTask<TInput>, DataflowTask>[] handlers)
        => instance.Split((_, i) => i, handlers);
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="splitHandler"></param>
    /// <param name="configurators"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Split<TInput, TOutput>(this DataflowTask<TInput> instance, SplitHandler<TInput> splitHandler, params Func<ConfiguredDataflowTask<TInput>, DataflowTask<TOutput>>[] configurators)
    {
        var items = configurators
            .Select(handler =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();

        var writers = items.Select(s => s.Writer).ToArray();
        
        var splitter = new DataflowSplitter<TInput>(splitHandler, writers);

        instance.AddTaskHandler(splitter.HandleAsync);
        return instance.Next(token =>
        {
            var sources = items
                .Select(s => s.Dataflow.SourceHandler(token))
                .ToArray();

            return AsyncEnumerable.MergeAsync(sources, cancellationToken: token);
        });
    }
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="configurators"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Split<TInput, TOutput>(this DataflowTask<TInput> instance, params Func<ConfiguredDataflowTask<TInput>, DataflowTask<TOutput>>[] configurators)
        => instance.Split((_, i) => i, configurators);
    
    
    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="splitHandler"></param>
    /// <param name="count"></param>
    /// <param name="handler"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Split<TInput, TOutput>(this DataflowTask<TInput> instance, SplitHandler<TInput> splitHandler, int count, Func<ConfiguredDataflowTask<TInput>, DataflowTask<TOutput>> handler)
    {
        var items = Enumerable
            .Range(0, count)
            .Select(_ =>
            {
                var configuredDataflow = new ConfiguredDataflowTask<TInput, TInput>(channel =>
                    channel.Reader.UseAsDataflowSource());

                var dataflow = handler(configuredDataflow);

                return new
                {
                    // ReSharper disable once RedundantAnonymousTypePropertyName
                    Writer = configuredDataflow.GetConfiguredChannel().Writer,
                    Dataflow = dataflow
                };
            })
            .ToList();

        var writers = items.Select(s => s.Writer).ToArray();
        
        var splitter = new DataflowSplitter<TInput>(splitHandler, writers);

        instance.AddTaskHandler(splitter.HandleAsync);
        return instance.Next(token =>
        {
            var sources = items
                .Select(s => s.Dataflow.SourceHandler(token))
                .ToArray();

            return AsyncEnumerable.MergeAsync(sources, cancellationToken: token);
        });
    }

    /// <summary>
    /// Passing data to each dataflow task in a round-robin fashion
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="count"></param>
    /// <param name="handler"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> Split<TInput, TOutput>(this DataflowTask<TInput> instance, int count, Func<ConfiguredDataflowTask<TInput>, DataflowTask<TOutput>> handler)
        => instance.Split((_, i) => i, count, handler);
    
    /// <summary>
    /// Pack data into batch
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="size"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TInput[]> Batch<TInput>(this DataflowTask<TInput> instance, int size)
    {
        return instance.Then<TInput, TInput[]>(new DataflowBatch<TInput>(size));
    }
    
    /// <summary>
    /// Pack data into batch
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="size"></param>
    /// <param name="timeout"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TInput[]> Batch<TInput>(this DataflowTask<TInput> instance, int size, TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfZero(timeout.Ticks, nameof(timeout));
        
        return instance
            .Then(new DataflowPeriodic<TInput>(timeout, true))
            .Then<TimedResult<TInput>, TInput[]>(new DataflowBatch<TInput>(size));
    }

    /// <summary>
    /// Interrupt flow with interval
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="period"></param>
    /// <param name="reset"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TimedResult<TInput>> Periodic<TInput>(this DataflowTask<TInput> instance, TimeSpan period, bool reset)
    {
        ArgumentOutOfRangeException.ThrowIfZero(period.Ticks, nameof(period));

        return instance.Then(new DataflowPeriodic<TInput>(period, reset));
    }
    
    /// <summary>
    /// Buffering data in background
    /// </summary>
    /// <param name="instance"></param>
    /// <typeparam name="TInput"></typeparam>
    /// <returns></returns>
    public static ConfiguredDataflowTask<TInput> Buffer<TInput>(this DataflowTask<TInput> instance)
    {
        return new ConfiguredDataflowTask<TInput, TInput>(channel =>
            instance.Then(new DataflowBuffer<TInput>(channel)));
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
    public static DataflowTask<TOutput> Transform<TInput, TOutput>(
        this DataflowTask<TInput> instance,
        Func<TInput, TOutput> mapper,
        Func<TInput, bool>? filter = null)
    {
        return instance.Then(new DataflowTransform<TInput, TOutput>(mapper, filter));
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
        return instance.Then(new DataflowTransform<TimedResult<TInput>, TOutput>(Map, Filter));

        TOutput Map(TimedResult<TInput> input) => mapper.Invoke(input.Value);
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
        Func<TimedResult<TInput>, bool>? filter = null)
    {
        return instance.Then(new DataflowTransform<TimedResult<TInput>, TimedResult<TOutput>>(Map, filter));

        TimedResult<TOutput> Map(TimedResult<TInput> input) => input.Expired
            ? new TimedResult<TOutput>(input.Timestamp)
            : new TimedResult<TOutput>(mapper.Invoke(input.Value), input.Timestamp);
    }
}