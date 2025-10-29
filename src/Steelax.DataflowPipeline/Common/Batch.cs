using System.Buffers;
using System.Collections;

namespace Steelax.DataflowPipeline.Common;

/// <summary>
/// Represents a batch of items stored in an array from ArrayPool for efficient memory management.
/// Similar to ArraySegment but with automatic pool-based allocation and disposal.
/// </summary>
/// <typeparam name="T">The type of elements in the batch</typeparam>
public sealed class Batch<T> : IDisposable, IReadOnlyList<T>
{
    private readonly int _length;
    private T[]? _array;
    private bool _disposed;

    /// <summary>
    /// Private constructor to control batch creation through factory methods
    /// </summary>
    /// <param name="array">The underlying array, can be null for empty batch</param>
    /// <param name="length">The number of valid elements in the array</param>
    private Batch(T[]? array, int length = 0)
    {
        _array = array;
        _length = length;
    }

    /// <summary>
    /// Gets an empty batch instance. This instance is shared and should not be disposed.
    /// </summary>
    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly Batch<T> Empty = new(null);

    /// <summary>
    /// Creates a new batch from the specified buffer by copying its contents to a pooled array.
    /// </summary>
    /// <param name="buffer">The source buffer to copy from</param>
    /// <returns>A new batch containing the copied data, or Empty if buffer is empty</returns>
    public static Batch<T> From(ReadOnlySpan<T> buffer)
    {
        if (buffer.Length == 0)
            return Empty;
        
        // Rent an array from the pool with at least the required capacity
        var array = ArrayPool<T>.Shared.Rent(buffer.Length);
        
        // Copy data from the buffer to the rented array
        buffer.CopyTo(array);

        // Store the actual length since ArrayPool.Rent returns an array with at least the requested size
        return new Batch<T>(array, buffer.Length);
    }

    /// <summary>
    /// Gets the element at the specified index in the batch.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get</param>
    /// <returns>The element at the specified index</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the batch is disposed</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range</exception>
    public T this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(index));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length, nameof(index));

            return _array![index];
        }
    }

    /// <summary>
    /// Gets a value indicating whether the batch contains no elements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the batch is disposed</exception>
    public bool IsEmpty
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));
            
            return _length == 0;
        }
    }

    /// <summary>
    /// Gets the number of elements contained in the batch.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the batch is disposed</exception>
    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));
            
            return _length;
        }
    }

    /// <summary>
    /// Returns a read-only span that represents the elements in the batch.
    /// </summary>
    /// <returns>A read-only span over the batch elements</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the batch is disposed</exception>
    public ReadOnlySpan<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));
        
        if (_length == 0)
            return ReadOnlySpan<T>.Empty;

        return _array.AsSpan(0, _length);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the batch.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the batch</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the batch is disposed</exception>
    public IEnumerator<T> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));

        if (_length == 0)
            yield break;
        
        for (var i = 0; i < _length; i++)
            yield return _array![i];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the batch.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the batch</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Finalizer to ensure the array is returned to the pool if Dispose is not called
    /// </summary>
    ~Batch()
    {
        Dispose(false);
    }
    
    /// <summary>
    /// Releases all resources used by the batch and returns the underlying array to the pool.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the batch and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        
        if (_array is not null)
        {
            _disposed = true;
            ArrayPool<T>.Shared.Return(_array, clearArray: true);
            _array = null;
        }
    }
}