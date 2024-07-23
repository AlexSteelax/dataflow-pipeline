using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowAggressiveBatch<TValue>(int size, TimeSpan timeout, Channel<TValue> channel) :
    IDataflowBackgroundTransform<TValue, TValue[]>
{
    private readonly Packer<TValue> _packer = new(size);

    public async IAsyncEnumerable<TValue[]> HandleAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        source.CancelAfter(timeout);

        TValue[]? buffer;

        while (!cancellationToken.IsCancellationRequested)
        {
            source.TryReset();

            var token = source.Token;
            
            bool bufferRelease;
            var breakLoop = false;

            try
            {
                var value = await channel.Reader.ReadAsync(token);

                bufferRelease = _packer.TryAddAndGet(value, out buffer);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == token)
            {
                bufferRelease = _packer.TryClearAndGet(out buffer);
            }
            catch (ChannelClosedException)
            {
                bufferRelease = _packer.TryClearAndGet(out buffer);
                breakLoop = true;
            }

            if (bufferRelease)
                yield return buffer!;

            if (breakLoop)
                break;
        }

        if (_packer.TryClearAndGet(out buffer))
            yield return buffer;
    }

    public async Task HandleAsync(IAsyncEnumerable<TValue> source, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var value in source.ConfigureAwait(false).WithCancellation(cancellationToken))
            {
                await channel.Writer.WriteAsync(value, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            channel.Writer.Complete();
        }
    }
}