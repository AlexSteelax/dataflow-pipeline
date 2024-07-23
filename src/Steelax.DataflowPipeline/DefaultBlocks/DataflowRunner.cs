using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowRunner<TInput> :
    IDataflowAction<TInput>
{
    public async Task HandleAsync(IAsyncEnumerable<TInput> source, CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            //Nothing
        }
    }
}