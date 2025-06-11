using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.DefaultBlocks.Algorithmic;

public abstract class DataflowChangeTracker<TMessage, TKey, TValue>(IEqualityComparer<TValue>? comparer = null) : IDataflowPipe<TMessage>
    where TKey : notnull
{
    private readonly IEqualityComparer<TValue> _comparer = comparer ?? EqualityComparer<TValue>.Default;
    
    private readonly Dictionary<TKey, TrackedValue<TValue>> _tracker = [];
    
    public async IAsyncEnumerable<TMessage> HandleAsync(IAsyncEnumerable<TMessage> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    break;
            }
            catch (ChannelClosedException)
            {
                break;
            }

            var message = enumerator.Current;
            
            var currentValue = DeconstructValue(message);
            var key = DeconstructKey(message);

            // Return current message on first occurrence of the key
            if (!_tracker.TryGetValue(key, out var previewValue))
            {
                _tracker[key] = currentValue;
                OnChanged(message, default);
                yield return message;
                continue;
            }

            // Skip current message if it's older than the tracked one
            if (currentValue.Timestamp < previewValue.Timestamp)
            {
                OnUnchanged(message, previewValue);
                continue;
            }

            // Skip current message if value hasn't changed
            if (_comparer.Equals(currentValue.Value, previewValue.Value))
            {
                _tracker[key] = currentValue;
                OnUnchanged(message, previewValue);
                continue;
            }
            
            // Return current message if value has changed
            _tracker[key] = currentValue;
            OnChanged(message, previewValue);
            yield return message;
        }
    }

    /// <summary>
    /// Deconstruct message into tracked-value
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected abstract TrackedValue<TValue> DeconstructValue(TMessage message);
    
    /// <summary>
    /// Deconstruct message into key-value
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    protected abstract TKey DeconstructKey(TMessage message);

    /// <summary>
    /// Remove tracked-value by key
    /// </summary>
    /// <param name="key"></param>
    protected void RemoveBy(TKey key) => _tracker.Remove(key);

    /// <summary>
    /// Fire callback if not same tracked value
    /// </summary>
    /// <param name="message"></param>
    /// <param name="previousValue"></param>
    protected virtual void OnChanged(TMessage message, TrackedValue<TValue> previousValue) { }
    
    /// <summary>
    /// Fire callback if same tracked value
    /// </summary>
    /// <param name="message"></param>
    /// <param name="previousValue"></param>
    protected virtual void OnUnchanged(TMessage message, TrackedValue<TValue> previousValue) { }
}