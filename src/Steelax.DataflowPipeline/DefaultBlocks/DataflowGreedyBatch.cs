using Steelax.DataflowPipeline.Abstractions;
using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowGreedyBatch<TValue>(int size) :
    IDataflowTransform<TValue, TValue[]>
{
    private readonly Packer<TValue> _packer = new(size);

    public async IAsyncEnumerable<TValue[]> HandleAsync(IAsyncEnumerable<TValue> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        TValue[]? buffer;

        await foreach (var value in source.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            if (_packer.TryAddAndGet(value, out buffer))
            {
                yield return buffer;
            }
        }

        if (_packer.TryClearAndGet(out buffer))
            yield return buffer;
    }
}