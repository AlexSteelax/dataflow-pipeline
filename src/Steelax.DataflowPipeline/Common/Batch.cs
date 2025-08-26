using System.Buffers;
using System.Collections;

namespace Steelax.DataflowPipeline.Common;

public sealed class Batch<T> : IDisposable, IReadOnlyList<T>
{
    private readonly int _length;
    private T[]? _array;
    private bool _disposed;

    private Batch(T[]? array, int length = 0)
    {
        _array = array;
        _length = length;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static readonly Batch<T> Empty = new(null);

    public static Batch<T> From(T[] buffer, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer, nameof(buffer));
        
        ArgumentOutOfRangeException.ThrowIfNegative(length, nameof(length));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, buffer.Length, nameof(length));
        
        if (length == 0)
            return Empty;
        
        var array = ArrayPool<T>.Shared.Rent(length);
        
        Array.Copy(buffer, 0, array, 0, length);

        return new Batch<T>(array, length);
    }

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
    
    public bool IsEmpty => _length == 0;

    public int Count
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));
            
            return _length;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(Batch<T>));

        if (_length == 0)
            yield break;
        
        for (var i = 0; i < _length; i++)
            yield return _array![i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    ~Batch()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        
        if (_array is not null)
            ArrayPool<T>.Shared.Return(_array, true);
        
        _array = null;
        _disposed = true;
    }
}