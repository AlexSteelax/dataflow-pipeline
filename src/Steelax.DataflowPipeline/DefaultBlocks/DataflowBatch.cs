using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal sealed class DataflowBatch<TValue>(int size) :
    IDataflowTransform<TValue, TValue[]>,
    IDataflowTransform<TimedResult<TValue>, TValue[]>
{
    public async IAsyncEnumerable<TValue[]> HandleAsync(IAsyncEnumerable<TValue> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var packer = new Packer<TValue>(size);
        
        TValue[]? buffer;
        
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        Exception? exception = null;

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
            catch (Exception ex)
            {
                exception = ex;
                moveNext = false;
            }

            if (!moveNext)
                break;
            
            var result = enumerator.Current;
            
            if (packer.TryAddAndGet(result, out buffer))
                yield return buffer;
        }
        
        if (packer.TryClearAndGet(out buffer))
            yield return buffer;
        
        if (exception is not null)
            throw exception;
    }

    public async IAsyncEnumerable<TValue[]> HandleAsync(IAsyncEnumerable<TimedResult<TValue>> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var packer = new Packer<TValue>(size);
        
        TValue[]? buffer;
        
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        Exception? exception = null;

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
            catch (Exception ex)
            {
                exception = ex;
                moveNext = false;
            }

            if (!moveNext)
                break;

            var result = enumerator.Current;

            if (result.Expired)
            {
                _ = packer.TryClearAndGet(out buffer);
                yield return buffer ?? [];
            }
            else if (packer.TryAddAndGet(result.Value, out buffer))
                yield return buffer;
        }
        
        if (packer.TryClearAndGet(out buffer))
            yield return buffer;
        
        if (exception is not null)
            throw exception;
    }
}