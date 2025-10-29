using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Steelax.DataflowPipeline.Common;

/// <summary>
/// Buffers items of type <typeparamref name="TValue"/> and packages them into <see cref="Batch{TValue}"/> instances
/// when the buffer reaches capacity or when explicitly requested.
/// 
/// This class provides efficient batching of items for bulk processing in dataflow pipelines,
/// reducing the overhead of processing individual items.
/// </summary>
/// <typeparam name="TValue">The type of items to buffer and batch</typeparam>
/// <remarks>
/// The packer maintains an internal buffer of fixed capacity. When the buffer becomes full,
/// it automatically creates a <see cref="Batch{TValue}"/> containing a copy of all buffered items
/// and clears the buffer for new items. Batches use ArrayPool for efficient memory management.
/// </remarks>
public class Packer<TValue>
{
    private readonly TValue[] _buffer;
    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="Packer{TValue}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of items the packer can buffer before creating a batch</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is less than 1</exception>
    public Packer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        
        _buffer = new TValue[capacity];
    }
    
    /// <summary>
    /// Gets a value indicating whether the buffer contains no items.
    /// </summary>
    public bool IsEmpty => _counter == 0;

    // CRITICAL: This private property checks if the buffer has reached its maximum capacity.
    // The packer will create a batch whenever this condition becomes true.
    private bool IsFull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _counter == _buffer.Length;
    }

    /// <summary>
    /// Attempts to add an item to the buffer and may return a batch if the buffer becomes full.
    /// </summary>
    /// <param name="value">The item to add to the buffer</param>
    /// <param name="batch">
    /// When this method returns, contains a <see cref="Batch{TValue}"/> if the buffer was full
    /// after adding the item; otherwise, contains <see cref="Batch{TValue}.Empty"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if a batch was created and returned; <c>false</c> if the item was simply added to the buffer.
    /// </returns>
    /// <remarks>
    /// IMPORTANT: This method handles two scenarios:
    /// 1. If the buffer is already full before adding the new item, it creates a batch from the current contents,
    ///    clears the buffer, then adds the new item to the empty buffer.
    /// 2. If the buffer becomes full after adding the new item, it creates a batch and clears the buffer.
    /// 
    /// Note that Batch.From() creates a COPY of the buffer contents using ArrayPool, so the original
    /// buffer can be safely cleared and reused without affecting the returned batch.
    /// </remarks>
    public bool TryEnqueue(TValue value, [MaybeNullWhen(false)] out Batch<TValue> batch)
    {
        // SCENARIO 1: Buffer is already full before adding the new item
        if (IsFull)
        {
            // Create a batch from all currently buffered items
            batch = Get();
            // Clear the buffer to make room for new items
            Clear();
            // Add the new item to the now-empty buffer
            Add(value);
            return true;
        }

        // Add the new item to the buffer
        Add(value);

        // SCENARIO 2: Buffer became full after adding the new item
        if (IsFull)
        {
            // Create a batch from the now-full buffer
            batch = Get();
            // Clear the buffer for future items
            Clear();
            return true;
        }

        // Buffer has room for more items, no batch created
        batch = null;
        return false;
    }

    /// <summary>
    /// Attempts to get a batch containing all currently buffered items and clear the buffer.
    /// </summary>
    /// <param name="batch">
    /// When this method returns, contains a <see cref="Batch{TValue}"/> with all buffered items
    /// if the buffer was not empty; otherwise, contains <see cref="Batch{TValue}.Empty"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the buffer contained items and a batch was created; <c>false</c> if the buffer was empty.
    /// </returns>
    /// <remarks>
    /// This method is typically used to flush any remaining items from the buffer when processing is complete
    /// or when forcing batch creation before the buffer is full.
    /// </remarks>
    public bool TryClear([MaybeNullWhen(false)] out Batch<TValue> batch)
    {
        if (IsEmpty)
        {
            batch = null;
            return false;
        }
        
        batch = Get();
        Clear();
        return true;
    }
    
    /// <summary>
    /// Adds an item to the buffer at the current position and increments the counter.
    /// </summary>
    /// <param name="value">The item to add</param>
    /// <remarks>
    /// CRITICAL: This method does not check buffer bounds. Callers must ensure the buffer
    /// has capacity (check IsFull) before calling this method to prevent index out of range exceptions.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Add(TValue value)
    {
        _buffer[_counter] = value;
        _counter += 1;
    }

    /// <summary>
    /// Creates a batch from the current buffer contents.
    /// </summary>
    /// <returns>
    /// A <see cref="Batch{TValue}"/> containing a copy of all buffered items, 
    /// or <see cref="Batch{TValue}.Empty"/> if the buffer is empty.
    /// </returns>
    /// <remarks>
    /// IMPORTANT: Batch.From() creates a COPY of the buffer contents using ArrayPool.
    /// The returned batch is independent of the packer's internal buffer and will not be
    /// affected by subsequent Clear() calls or additions to the buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Batch<TValue> Get() => Batch<TValue>.From(_buffer.AsSpan(0, _counter));

    /// <summary>
    /// Clears the buffer by resetting the counter and optionally clearing array elements.
    /// </summary>
    /// <remarks>
    /// CRITICAL: This method clears the internal buffer but does NOT affect any batches
    /// that were previously created using Get(). Those batches contain independent copies
    /// of the data and remain valid.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Clear()
    {
        // Clear the array elements to help garbage collection and prevent memory leaks
        // in case TValue is a reference type with disposable resources
        Array.Clear(_buffer, 0, _counter);
        _counter = 0;
    }
}