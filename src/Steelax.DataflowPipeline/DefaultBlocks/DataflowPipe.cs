using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowPipe<TInput, TOutput>(Func<TInput, TOutput> mapper, Func<TInput, bool>? filter = null) :
    IDataflowPipe<TInput, TOutput>
{
    public async IAsyncEnumerable<TOutput> HandleAsync(IAsyncEnumerable<TInput> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            bool moveNext;

            try
            {
                moveNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                moveNext = false;
            }
            
            if (!moveNext)
                break;

            var result = enumerator.Current;

            if (!filter?.Invoke(result) ?? false)
                continue;

            yield return mapper.Invoke(result);
        }
    }
}