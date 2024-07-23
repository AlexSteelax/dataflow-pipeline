using System.Diagnostics.CodeAnalysis;

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
    /// <param name="buffer"></param>
    /// <returns></returns>
    public bool TryAddAndGet(TValue value, [MaybeNullWhen(false)] out TValue[] buffer)
    {
        if (IsFull)
        {
            buffer = Get();
            Clear();
            Add(value);
            return true;
        }

        Add(value);

        if (IsFull)
        {
            buffer = Get();
            Clear();
            return true;
        }

        buffer = null;
        return false;
    }

    /// <summary>
    /// Clear buffer if not empty and return it
    /// </summary>
    /// <param name="buffer"></param>
    /// <returns></returns>
    public bool TryClearAndGet([MaybeNullWhen(false)] out TValue[] buffer)
    {
        if (IsEmpty)
        {
            buffer = null;
            return false;
        }

        buffer = Get();
        Clear();
        return true;
    }
    
    private bool IsFull => _counter == _buffer.Length;

    private void Add(TValue value)
    {
        _buffer[_counter] = value;
        _counter += 1;
    }

    private TValue[] Get()
    {
        if (IsEmpty)
            return Array.Empty<TValue>();

        var res = new TValue[_counter];

        Array.Copy(_buffer, 0, res, 0, _counter);

        return res;
    }

    private void Clear()
    {
        Array.Clear(_buffer, 0, _counter);
        _counter = 0;
    }
}
