using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;

namespace Steelax.DataflowPipeline.Common;

internal class Packer<TValue>
{
    private readonly TValue[] _buffer;
    private int _counter;

    public Packer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        
        _buffer = new TValue[capacity];
    }
    
    /// <summary>
    /// Check buffer is empty
    /// </summary>
    public bool IsEmpty => _counter == 0;

    /// <summary>
    /// Add value into buffer and return buffer if it becomes full
    /// </summary>
    /// <param name="value"></param>
    /// <param name="batch"></param>
    /// <returns></returns>
    public bool TryAddAndGet(TValue value, out IMemoryOwner<TValue> batch)
    {
        if (IsFull)
        {
            batch = Get();
            Clear();
            Add(value);
            return true;
        }

        Add(value);

        if (IsFull)
        {
            batch = Get();
            Clear();
            return true;
        }

        batch = MemoryOwner<TValue>.Empty;
        return false;
    }

    /// <summary>
    /// Clear buffer if not empty and return it
    /// </summary>
    /// <param name="batch"></param>
    /// <returns></returns>
    public bool TryClearAndGet(out IMemoryOwner<TValue> batch)
    {
        if (IsEmpty)
        {
            batch = MemoryOwner<TValue>.Empty;
            return false;
        }

        batch = Get();
        Clear();
        return true;
    }
    
    private bool IsFull => _counter == _buffer.Length;

    private void Add(TValue value)
    {
        _buffer[_counter] = value;
        _counter += 1;
    }

    private IMemoryOwner<TValue> Get()
    {
        if (IsEmpty)
            return MemoryOwner<TValue>.Empty;

        var res = MemoryOwner<TValue>.Allocate(_counter, AllocationMode.Clear);

        _buffer.AsSpan(0, _counter).CopyTo(res.Span);

        return res;
    }

    private void Clear()
    {
        Array.Clear(_buffer, 0, _counter);
        _counter = 0;
    }
}
