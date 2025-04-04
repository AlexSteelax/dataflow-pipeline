namespace Steelax.DataflowPipeline.Extensions;

internal static partial class AsyncEnumerable
{
    private static IAsyncEnumerable<T> Empty<T>() => EmptyAsyncEnumerator<T>.Instance;

    private class EmptyAsyncEnumerator<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        public static readonly EmptyAsyncEnumerator<T> Instance = new();
        public T Current => default!;
        public ValueTask DisposeAsync() => default;
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this;
        }
        public ValueTask<bool> MoveNextAsync() => new(false);
    }
}