using JetBrains.Annotations;

namespace Steelax.DataflowPipeline.Async;

/// <summary>
/// Builder for creating configured instances of MergeAsyncEnumerable.
/// Uses ref struct to enforce single-use pattern and prevent lifetime issues.
/// </summary>
/// <typeparam name="T">The type of elements in the sequence</typeparam>
public readonly ref struct AsyncMergeBuilder<T>
{
    private readonly IAsyncEnumerable<T>[] _sources;
    private readonly FaultToleranceMode _mode;
    private readonly CancellationToken _stoppingToken;
    
    private AsyncMergeBuilder(IAsyncEnumerable<T>[] sources, FaultToleranceMode mode, CancellationToken stoppingToken)
    {
        _sources = sources;
        _mode = mode;
        _stoppingToken = stoppingToken;
    }

    /// <summary>
    /// Creates a new builder with the specified sources.
    /// Default configuration: Strict fault tolerance, no cancellation token.
    /// </summary>
    [PublicAPI]
    public static AsyncMergeBuilder<T> UseSource(params IAsyncEnumerable<T>[] sources) =>
        new(sources, FaultToleranceMode.Strict, CancellationToken.None);
    
    /// <summary>
    /// Append specified sources
    /// </summary>
    [PublicAPI]
    public AsyncMergeBuilder<T> AppendSources(params IAsyncEnumerable<T>[] sources) =>
        new (_sources.AsEnumerable().Union(sources).ToArray(), _mode, _stoppingToken);
    
    /// <summary>
    /// Configures the fault tolerance strategy for the merge operation.
    /// </summary>
    [PublicAPI]
    public AsyncMergeBuilder<T> WithFaultStrategy(FaultToleranceMode mode) =>
        new (_sources, mode, _stoppingToken);
    
    /// <summary>
    /// Configures the global cancellation token for all enumerators.
    /// This token controls the lifetime of the entire MergeAsyncEnumerable.
    /// </summary>
    [PublicAPI]
    public AsyncMergeBuilder<T> WithCancellation(CancellationToken stoppingToken) =>
        new (_sources, _mode, stoppingToken);

    /// <summary>
    /// Builds a configured MergeAsyncEnumerable instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no sources are provided</exception>
    [PublicAPI]
    public AsyncMergeable<T> Build()
    {
        if (_sources == null || _sources.Length == 0)
            throw new InvalidOperationException("At least one source is required");
        
        return new AsyncMergeable<T>(_sources, _mode, _stoppingToken);
    }
}