using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Steelax.DataflowPipeline.Common;

public class ChunkedCircularBuffer<T>
{
    private readonly T[] _buffer;
    private readonly int _chunkSize;
    private readonly int _capacity;
    private readonly int[] _chunkLocks;
    
    private int _head;
    private int _tail;
    private int _count;

    public ChunkedCircularBuffer(int capacity, int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        _capacity = capacity;
        _chunkSize = chunkSize;
        _buffer = new T[capacity * chunkSize];
        _chunkLocks = new int[capacity]; // 0 = unlocked, 1 = locked
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    public int ChunkSize => _chunkSize;
    public int Capacity => _capacity;
    public int AvailableChunks => _count;
    public bool IsEmpty => _count == 0;
    public bool IsFull => _count == _capacity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryLockChunk(int index) => Interlocked.CompareExchange(ref _chunkLocks[index], 1, 0) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UnlockChunk(int index) => Interlocked.Exchange(ref _chunkLocks[index], 0);

    
    public bool TryWriteChunk(ReadOnlySpan<T> data)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(data.Length, _chunkSize, nameof(data.Length));

        int currentTail;
        int nextTail;

        // Atomic check and reserve space
        do
        {
            currentTail = _tail;
            var currentCount = _count;
            
            if (currentCount == _capacity)
                return false;

            nextTail = (currentTail + 1) % _capacity;
        } 
        while (Interlocked.CompareExchange(ref _tail, nextTail, currentTail) != currentTail);

        // Try to lock the chunk for writing
        if (!TryLockChunk(currentTail))
        {
            // Rollback tail if we can't lock
            Interlocked.CompareExchange(ref _tail, currentTail, nextTail);
            return false;
        }

        try
        {
            // Write data to the chunk
            var writeIndex = currentTail * _chunkSize;
            data.CopyTo(_buffer.AsSpan(writeIndex, _chunkSize));

            // Update count after successful write
            Interlocked.Increment(ref _count);
            return true;
        }
        finally
        {
            UnlockChunk(currentTail);
        }
    }

    public bool TryReadChunk(Span<T> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(destination.Length, _chunkSize, nameof(destination.Length));

        int currentHead;
        int nextHead;

        // Atomic check and reserve chunk for reading
        do
        {
            currentHead = _head;
            var currentCount = _count;
            
            if (currentCount == 0)
                return false;

            nextHead = (currentHead + 1) % _capacity;
        } 
        while (Interlocked.CompareExchange(ref _head, nextHead, currentHead) != currentHead);

        // Try to lock the chunk for reading
        if (!TryLockChunk(currentHead))
        {
            // Rollback head if we can't lock
            Interlocked.CompareExchange(ref _head, currentHead, nextHead);
            return false;
        }

        try
        {
            // Read data from the chunk
            var readIndex = currentHead * _chunkSize;
            _buffer.AsSpan(readIndex, _chunkSize).CopyTo(destination);

            // Update count after successful read
            Interlocked.Decrement(ref _count);
            return true;
        }
        finally
        {
            UnlockChunk(currentHead);
        }
    }

    public bool TryGetElement(int index, int offset, [MaybeNullWhen(false)] out T value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _capacity);

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, _chunkSize);

        if (!TryLockChunk(index))
        {
            value = default;
            return false;
        }

        try
        {
            var absoluteIndex = index * _chunkSize + offset;
            value = _buffer[absoluteIndex];
            return true;
        }
        finally
        {
            UnlockChunk(index);
        }
    }

    public bool TrySetElement(int index, int offset, T value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _capacity);

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, _chunkSize);

        if (!TryLockChunk(index))
            return false;

        try
        {
            var absoluteIndex = index * _chunkSize + offset;
            _buffer[absoluteIndex] = value;
            return true;
        }
        finally
        {
            UnlockChunk(index);
        }
    }

    public bool TryPeekChunk(int chunksAhead, Span<T> destination)
    {
        if (chunksAhead < 0 || chunksAhead >= _count)
            return false;
        if (destination.Length != _chunkSize)
            throw new ArgumentException($"Destination length must be exactly {_chunkSize}", nameof(destination));

        var peekChunkIndex = (_head + chunksAhead) % _capacity;

        if (!TryLockChunk(peekChunkIndex))
            return false;

        try
        {
            var readIndex = peekChunkIndex * _chunkSize;
            _buffer.AsSpan(readIndex, _chunkSize).CopyTo(destination);
            return true;
        }
        finally
        {
            UnlockChunk(peekChunkIndex);
        }
    }

    public bool TryAccessChunk(int index, out ChunkScope chunkScope)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _capacity);

        if (!TryLockChunk(index))
        {
            chunkScope = default;
            return false;
        }

        chunkScope = new ChunkScope(this, index, _chunkSize);
        return true;

    }

    public void Clear()
    {
        // Clear all chunks sequentially
        for (var i = 0; i < _capacity; i++)
        {
            if (!TryLockChunk(i))
                continue;
            
            try
            {
                var startIndex = i * _chunkSize;
                Array.Clear(_buffer, startIndex, _chunkSize);
            }
            finally
            {
                UnlockChunk(i);
            }
        }

        // Reset counters
        Interlocked.Exchange(ref _head, 0);
        Interlocked.Exchange(ref _tail, 0);
        Interlocked.Exchange(ref _count, 0);
    }

    public readonly struct ChunkScope : IDisposable
    {
        private readonly ChunkedCircularBuffer<T> _instance;
        private readonly int _index;
        private readonly int _chunkSize;

        public Span<T> Span => _instance._buffer.AsSpan(_index * _chunkSize, _chunkSize);

        internal ChunkScope(ChunkedCircularBuffer<T> instance, int index, int chunkSize)
        {
            _instance = instance;
            _index = index;
            _chunkSize = chunkSize;
        }
        
        public void Dispose()
        {
            _instance?.UnlockChunk(_index);
        }
    }
}