using System.Runtime.CompilerServices;
using DotNext.Threading;
using DotNext.Threading.Tasks;
using JetBrains.Annotations;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ExtractCommonBranchingCode

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

/// <summary>
/// Merges multiple IAsyncEnumerable sources into a single async sequence with configurable fault tolerance.
/// Features: non-thread-safe, efficient round-robin, fault-tolerant enumeration.
/// </summary>
/// <typeparam name="T">The type of elements in the sequence</typeparam>
public sealed class AsyncMergeable<T> : IAsyncEnumerable<T>, IAsyncDisposable
{
    private readonly CancellationToken _stoppingToken;
    private readonly IAsyncEnumerator<T>[] _enumerators;
    private readonly ValueTask<bool>[] _moves;
    private readonly AsyncAutoResetEvent _autoResetEvent;
    private readonly Indexer _indexer;
    private bool _disposed;
    
    /// <summary>
    /// Gets the configured fault tolerance mode for this merge operation.
    /// </summary>
    [PublicAPI]
    public FaultToleranceMode FaultToleranceMode { get; private init; }
    
    internal AsyncMergeable(IAsyncEnumerable<T>[] sources, FaultToleranceMode faultToleranceMode, CancellationToken stoppingToken)
    {
        FaultToleranceMode = faultToleranceMode;
        
        _stoppingToken = stoppingToken;
        _enumerators = new IAsyncEnumerator<T>[sources.Length];
        _moves = new ValueTask<bool>[sources.Length];
        _autoResetEvent = new AsyncAutoResetEvent(false);
        _indexer = new Indexer(sources.Length);

        // Initialize all sources and start their first MoveNextAsync operation
        for (var i = 0; i < sources.Length; i++)
        {
            _enumerators[i] = sources[i].GetAsyncEnumerator(stoppingToken);
            MoveNext(i);
        }
    }

    /// <summary>
    /// Critical: Starts the next MoveNextAsync operation for a source and registers completion callback.
    /// This enables overlapping I/O - the next async operation starts while the current element is processed.
    /// </summary>
    private void MoveNext(int index)
    {
#pragma warning disable CA2012 // Use ValueTask correctly - we're handling completion explicitly
        var task = _moves[index] = _enumerators[index].MoveNextAsync();
#pragma warning restore CA2012

        // Register completion callback if not already completed
        if (task.GetAwaiter() is { IsCompleted: false } awaiter)
        {
            awaiter.OnCompleted(OnSourceReady);
        }
        else
        {
            // Immediate completion - signal availability
            OnSourceReady();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T GetCurrent(int index) => _enumerators[index].Current;

    /// <summary>
    /// Callback invoked when any source's MoveNextAsync operation completes.
    /// Signals the AutoResetEvent to wake up waiting consumers.
    /// Exception-safe: suppresses exceptions during disposal scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnSourceReady()
    {
        try
        {
            _autoResetEvent.Set();
        }
        catch
        {
            // Expected during disposal - AutoResetEvent may be disposed
            // before all pending callbacks complete
        }
    }
    
    /// <summary>
    /// Creates a new enumerator for consuming the merged sequence.
    /// Multiple enumerators can be created but should not be used concurrently.
    /// </summary>
    [PublicAPI]
    public Enumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new (this, cancellationToken);
    
    /// <summary>
    /// Creates a new enumerator for consuming the merged sequence.
    /// Multiple enumerators can be created but should not be used concurrently.
    /// </summary>
    [PublicAPI]
    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken)
        => new Enumerator(this, cancellationToken);
    
    /// <summary>
    /// Coordinated disposal of all resources.
    /// Safe to call multiple times - implements idempotent disposal pattern.
    /// </summary>
    [PublicAPI]
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        // Phase 1: Dispose synchronization primitive to release any waiting consumers
        await _autoResetEvent.DisposeAsync().ConfigureAwait(false);

        // Phase 2: Dispose all source enumerators with exception protection
        for (var i = 0; i < _enumerators.Length; i++)
        {
            _moves[i] = default; // Clear task references

            try
            {
                await _enumerators[i].DisposeAsync().ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                // Some enumerators (particularly compiler-generated async iterators)
                // may not support DisposeAsync - this is expected and safe to ignore
            }
        }
        
        // Phase 3: Dispose indexer
        _indexer.Dispose();
    }

    /// <summary>
    /// The consumer-facing enumerator that implements the merge logic.
    /// Maintains faulted state: once an exception occurs, all subsequent calls throw the same exception.
    /// </summary>
    public sealed class Enumerator(AsyncMergeable<T> mergeable, CancellationToken cancellationToken) : IAsyncEnumerator<T>
    {
        private Exception? _faultException;
        private T? _current;
        private int _index = -1;
        private bool _init;
        
        /// <summary>
        /// Minimal disposal - the parent MergeAsyncEnumerable handles resource cleanup.
        /// This prevents double disposal of shared resources.
        /// </summary>
        [PublicAPI]
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Core merge algorithm: Uses Indexer for round-robin through active sources.
        /// Implements configurable fault tolerance and maintains faulted state.
        /// </summary>
        [PublicAPI]
        public async ValueTask<bool> MoveNextAsync()
        {
            // Critical: Once faulted, always throw the same exception
            // This matches standard IAsyncEnumerator behavior
            if (_faultException is not null)
                throw _faultException;

            _init = true;
            
            // Cache frequently accessed fields for performance
            var indexer = mergeable._indexer;
            var moves = mergeable._moves;
            var autoResetEvent = mergeable._autoResetEvent;
            
            while (true)
            {
                ThrowIfCancellationRequested();
                
                // Use Indexer to get next active index in round-robin fashion
                // Returns -1 when no active sources remain
                var index = indexer.Forward();
                
                if (index == -1)
                {
                    return false; // All sources completed or removed
                }
                
                var task = moves[index];
                
                if (task.IsCompleted)
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        if (task.Result)
                        {
                            // SUCCESS: Source has data available
                            _index = index;
                            _current = mergeable.GetCurrent(index);
                            
                            // Critical: Restart source before yielding to overlap I/O
                            mergeable.MoveNext(index);
                            return true;
                        }

                        // Source reached natural end - remove from active rotation
                        indexer.Remove();
                    }
                    else
                    {
                        // Task completed with error - apply fault tolerance strategy
                        mergeable.MoveNext(index);

                        switch (mergeable.FaultToleranceMode)
                        {
                            case FaultToleranceMode.Strict:
                                // Any exception is fatal - store and throw
                                _index = index;
                                _current = default!;
                                _faultException = task.AsTask().Exception!.GetBaseException();
                                throw _faultException;
                            case FaultToleranceMode.TolerantToCancellation:
                                if (task.IsFaulted)
                                {
                                    // Only cancellation is tolerated - rethrow other exceptions
                                    _index = index;
                                    _current = default!;
                                    _faultException = task.AsTask().Exception!.GetBaseException();
                                    throw _faultException;
                                }
                                // Cancellation is tolerated - remove source and continue
                                indexer.Remove();
                                break;
                            case FaultToleranceMode.Resilient:
                                // All exceptions are tolerated - remove source and continue
                                indexer.Remove();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
                else
                {
                    // Source not ready - wait for signal from any source
                    try
                    {
                        await autoResetEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        ThrowIfCancellationRequested();
                        return false;
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks both local and global cancellation tokens.
        /// Global token always takes precedence and indicates complete shutdown.
        /// </summary>
        private void ThrowIfCancellationRequested()
        {
            cancellationToken.ThrowIfCancellationRequested();
            mergeable._stoppingToken.ThrowIfCancellationRequested();
        }
        
        /// <summary>
        /// Gets the current element. Throws if enumeration hasn't started or is faulted.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when enumeration hasn't started</exception>
        /// <exception cref="Exception">Thrown when enumerator is in faulted state</exception>
        [PublicAPI]
        public T Current
        {
            get
            {
                if (!_init)
                    throw new InvalidOperationException("Enumeration has not started. Call MoveNextAsync.");
                
                if (_faultException is not null)
                    throw _faultException;

                return _current!;
            }
        }

        /// <summary>
        /// Gets the index of the source that produced the current element.
        /// Returns -1 if no current element or enumeration hasn't started.
        /// </summary>
        [PublicAPI]
        public int Index => _index;
    }
}

/// <summary>
/// Defines how the merge operation handles exceptions from individual sources.
/// </summary>
public enum FaultToleranceMode
{
    /// <summary>
    /// Any exception (including OperationCanceledException) immediately stops the entire operation.
    /// Use case: Critical systems where partial results are unacceptable.
    /// </summary>
    Strict,

    /// <summary>
    /// OperationCanceledException is absorbed (source is removed), other exceptions stop the operation.
    /// Use case: Systems requiring graceful degradation during temporary cancellations.
    /// </summary>
    TolerantToCancellation,

    /// <summary>
    /// All exceptions are absorbed, operation continues with remaining sources.
    /// Use case: Best-effort data collection, monitoring, analytics.
    /// </summary>
    Resilient
}