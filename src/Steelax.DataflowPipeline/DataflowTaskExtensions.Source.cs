using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline;

public static partial class DataflowTaskExtensions
{
    /// <summary>
    /// Use dataflow blocks as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this IEnumerable<IDataflowBreader<TOutput>> input)
    {
        return DataflowTask<TOutput>.CreateNew(token =>
        {
            var streams = input
                .Select(s => s.HandleAsync(token))
                .ToArray();
                
            return AsyncEnumerable.MergeAsync(streams, cancellationToken: token);
        });
    }

    /// <summary>
    /// Use dataflow block as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this IDataflowBreader<TOutput> input) =>
        UseAsDataflowSource([input]);
    
    /// <summary>
    /// Use streams as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this IEnumerable<IAsyncEnumerable<TOutput>> input)
    {
        var sources = input.ToArray();
        
        return DataflowTask<TOutput>.CreateNew(token =>
            AsyncEnumerable.MergeAsync(sources, cancellationToken: token));
    }

    /// <summary>
    /// Use stream as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this IAsyncEnumerable<TOutput> input) =>
        UseAsDataflowSource([input]);
    
    /// <summary>
    /// Use channels as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this IEnumerable<ChannelReader<TOutput>> input)
    {
        return DataflowTask<TOutput>.CreateNew(token =>
        {
            var streams = input
                .Select(s => s.ReadAllAsync(token))
                .ToArray();
            
            return AsyncEnumerable.MergeAsync(streams, cancellationToken: token);
        });
    }

    /// <summary>
    /// Use channel as source
    /// </summary>
    /// <param name="input"></param>
    /// <typeparam name="TOutput"></typeparam>
    /// <returns></returns>
    // ReSharper disable once MemberCanBePrivate.Global
    public static DataflowTask<TOutput> UseAsDataflowSource<TOutput>(this ChannelReader<TOutput> input) =>
        UseAsDataflowSource([input]);
}