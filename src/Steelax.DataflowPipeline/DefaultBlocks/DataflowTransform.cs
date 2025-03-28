using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowTransform<TInput, TOutput>(Func<TInput, TOutput> mapHandler) :
    IDataflowTransform<TInput, TOutput>,
    IDataflowTransform<TimedResult<TInput>, TOutput>
{
    public async IAsyncEnumerable<TOutput> HandleAsync(IAsyncEnumerable<TInput> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            bool moveNext;

            try
            {
                moveNext = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                moveNext = false;
            }
            
            if (!moveNext)
                break;

            yield return mapHandler.Invoke(enumerator.Current);
        }
    }

    public async IAsyncEnumerable<TOutput> HandleAsync(IAsyncEnumerable<TimedResult<TInput>> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            bool moveNext;

            try
            {
                moveNext = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                moveNext = false;
            }
            
            if (!moveNext)
                break;
            
            var result = enumerator.Current;

            if (result.Empty)
                continue;

            yield return mapHandler.Invoke(result.Value);
        }
    }
}