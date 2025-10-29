namespace Steelax.DataflowPipeline.Abstractions;

public interface IAsyncEnumerableMerger<T>
{
    IAsyncEnumerable<T> MergeAsync(IAsyncEnumerable<T>[] sources, CancellationToken cancellationToken);
}