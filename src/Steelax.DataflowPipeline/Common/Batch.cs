using System.Buffers;

namespace Steelax.DataflowPipeline.Common;

public readonly struct Batch<T>(int size, IMemoryOwner<T>? memoryOwner = null) : IDisposable
{
    public ReadOnlySpan<T> Span => memoryOwner is not null
        ? memoryOwner.Memory.Span[..size]
        : new ReadOnlySpan<T>([]);

    public void Dispose() =>
        memoryOwner?.Dispose();

    public static readonly Batch<T> Empty = new();
}