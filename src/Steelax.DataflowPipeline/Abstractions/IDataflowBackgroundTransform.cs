namespace Steelax.DataflowPipeline.Abstractions;

public interface IDataflowBackgroundTransform<in TInput, out TOutput>
{
    /// <summary>
    /// Consume and handle input values
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken);
    
    /// <summary>
    /// Produce output values
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<TOutput> HandleAsync(CancellationToken cancellationToken);
}