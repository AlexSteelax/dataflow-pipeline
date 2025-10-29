using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Steelax.DataflowPipeline.Common;

/// <summary>
/// Represents a high-performance index manager that maintains a collection of indices
/// with efficient activation/deactivation capabilities. Optimized for small collections
/// (typically ~10 elements) with minimal memory overhead and cache-friendly layout.
/// </summary>
public sealed unsafe class Indexer : IDisposable
{
    private int* _memory;        // Pointer to contiguous memory block: [header][data...]
    private bool _isAllocated;   // Track allocation state for safe disposal
    
    // Header offsets within the memory block
    private const int HeaderCurrentOffset = 0;   // int: Current active index position
    private const int HeaderCapacityOffset = 1;  // int: Maximum capacity of indices
    private const int HeaderSize = 2;            // Total header size in integers (2 × sizeof(int))
    
    // Bit masks for efficient state management
    private const int InactiveMask = 1 << 31;    // MSB flag indicating inactive state
    private const int ValueMask = 0x7FFFFFFF;    // Mask to extract actual index value

    private const int InitialPosition = -2;      // Special initial position
    private const int EndOfIndex = -1;           // No active indices found
    
    /// <summary>
    /// Provides reference access to the Current field in the header
    /// </summary>
    private ref int Current => ref _memory[HeaderCurrentOffset];
    
    /// <summary>
    /// Provides reference access to the Capacity field in the header
    /// </summary>
    private ref int Capacity => ref _memory[HeaderCapacityOffset];
    
    /// <summary>
    /// Provides pointer access to the data section (after header)
    /// </summary>
    private int* Data => _memory + HeaderSize;
    
    /// <summary>
    /// Initializes a new Indexer instance with specified capacity
    /// </summary>
    /// <param name="capacity">Number of indices to manage. Must be at least 1.</param>
    /// <exception cref="ArgumentException">Thrown when capacity is less than 1</exception>
    /// <remarks>
    /// Memory layout: [Current][Capacity][Data0][Data1]...[DataN-1]
    /// All indices are initially active and values are set to 0..capacity-1
    /// </remarks>
    public Indexer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        
        // Allocate contiguous memory block for header and data
        var totalSize = HeaderSize + capacity;
        _memory = (int*)NativeMemory.Alloc((nuint)totalSize * sizeof(int));
        _isAllocated = true;
        
        // Initialize header fields
        Current = InitialPosition;   // Special initial position
        Capacity = capacity;    // Set maximum capacity
        
        // Initialize all indices as active with sequential values
        for (var i = 0; i < capacity; i++)
        {
            Data[i] = i; // Value equals index, MSB = 0 (active)
        }
    }
    
    /// <summary>
    /// Finalizer ensures native memory is released during garbage collection
    /// if Dispose() was not explicitly called
    /// </summary>
    ~Indexer()
    {
        Dispose(false);
    }
    
    /// <summary>
    /// Computes the next cyclic index from current position
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveForward(int current, int steps, int capacity)
    {
        var next = current + steps;
        return next % capacity;
    }
    
    /// <summary>
    /// Computes the previous cyclic index from current position
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MoveBackward(int current, int steps, int capacity)
    {
        var next = current - steps;
        return next >= 0 ? next : next % capacity + capacity;
    }
    
    /// <summary>
    /// Advances to the next active index and returns its value
    /// </summary>
    /// <returns>
    /// The value of the next active index, or -1 if no more active indices exist
    /// </returns>
    /// <remarks>
    /// Search starts from the current position + 1. Updates Current position internally.
    /// Automatically skips inactive indices (marked with MSB set).
    /// </remarks>
    public int Forward()
    {
        // Early exit if no active indices remain
        if (Current == EndOfIndex)
            return EndOfIndex;

        if (Current == InitialPosition)
            return Data[Current = 0];
        
        // Linear search for next active index starting from current position
        for (var offset = 1; offset <= Capacity; offset++)
        {
            var next = MoveForward(Current, offset, Capacity);
            
            // Skip indices marked as inactive (MSB set)
            if ((Data[next] & InactiveMask) != 0)
                continue;
            
            // Update current position and return active index value
            Current = next;
            return Data[next] & ValueMask;
        }
        
        // No active indices found - mark as exhausted
        Current = EndOfIndex;
        return EndOfIndex;
    }
    
    /// <summary>
    /// Moves to the previous active index and returns its value
    /// </summary>
    /// <returns>
    /// The value of the previous active index, or -1 if no more active indices exist
    /// </returns>
    /// <remarks>
    /// Search starts from the current position - 1. Updates Current position internally.
    /// Automatically skips inactive indices (marked with MSB set).
    /// </remarks>
    public int Preview()
    {
        // Early exit if no active indices remain
        if (Current == EndOfIndex)
            return EndOfIndex;

        if (Current == InitialPosition)
            return Data[Current = Capacity - 1];
        
        // Linear search for previous active index starting from current position
        for (var offset = 1; offset <= Capacity; offset++)
        {
            var next = MoveBackward(Current, offset, Capacity);
            
            // Skip indices marked as inactive (MSB set)
            if ((Data[next] & InactiveMask) != 0)
                continue;
            
            // Update current position and return active index value
            Current = next;
            return Data[next] & ValueMask;
        }
        
        // No active indices found - mark as exhausted
        Current = EndOfIndex;
        return EndOfIndex;
    }
    
    /// <summary>
    /// Marks the current index as inactive (logical removal)
    /// </summary>
    /// <remarks>
    /// Does not physically remove the index from memory. Subsequent calls to
    /// Next() or Preview() will automatically skip this index.
    /// The Current position remains unchanged after removal.
    /// </remarks>
    public void Remove()
    {
        // Early exit if no current index is selected
        if (Current == EndOfIndex)
            return;
        
        // Ensure current position is valid before modification
        if (Current >= 0 && Current < Capacity)
        {
            // Set MSB to mark as inactive while preserving the original value
            Data[Current] |= InactiveMask;
        }
    }
    
    /// <summary>
    /// Releases all native resources used by the Indexer
    /// </summary>
    /// <remarks>
    /// Implements IDisposable pattern. Safe to call multiple times.
    /// Suppresses finalization to prevent redundant cleanup.
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Protected implementation of Dispose pattern
    /// </summary>
    /// <param name="disposing">
    /// True if called explicitly via Dispose(), false if called from finalizer
    /// </param>
    private void Dispose(bool disposing)
    {
        // Only deallocate if memory was previously allocated and not already freed
        if (!_isAllocated || _memory == null)
            return;
        
        NativeMemory.Free(_memory);
        _memory = null;
        _isAllocated = false;
    }
}