using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Steelax.DataflowPipeline.Extensions;

internal static class AsyncEnumerable
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
    
    public static IAsyncEnumerable<T> MergeAsync<T>(
        IAsyncEnumerable<T>[] sources,
        bool thrownExceptionImmediately = false,
        CancellationToken cancellationToken = default)
    {
        return sources.Length switch
        {
            0 => Empty<T>(),
            1 => sources[0],
            _ => Core(sources, thrownExceptionImmediately, cancellationToken)
        };
        
        static async IAsyncEnumerable<T> Core(
            IAsyncEnumerable<T>[] sources,
            bool thrownExceptionImmediately,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = tokenSource.Token;
            
            var enumerators = new ConfiguredCancelableAsyncEnumerable<T>.Enumerator[sources.Length];
            
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                enumerators[i] = source.ConfigureAwait(false).WithCancellation(token).GetAsyncEnumerator();
            }

            var awaiters = new ConfiguredValueTaskAwaitable<bool>.ConfiguredValueTaskAwaiter[sources.Length];

            var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(sources.Length)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });
            
            long active = sources.Length;
            var exceptions = new List<Exception>();

            for (var i = 0; i < enumerators.Length; i++)
                MoveNext(i);

            await foreach (var index in channel.Reader.ReadAllAsync(default).ConfigureAwait(false))
            {
                var success = false;

                try
                {
                    success = awaiters[index].GetResult();
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == token)
                {
                    //nothing
                }
                catch(Exception ex)
                {
                    exceptions.Add(ex);
                    tokenSource.Cancel(true);

                    if (thrownExceptionImmediately)
                        break;
                }
                
                if (success)
                {
                    var ret = enumerators[index].Current;
                    
                    MoveNext(index);

                    yield return ret;
                }
                else
                {
                    Interlocked.Decrement(ref active);
                }

                if (Interlocked.Read(ref active) == 0)
                {
                    channel.Writer.TryComplete();
                }
            }

            foreach (var enumerator in enumerators)
            {
                await enumerator.DisposeAsync();
            }

            Array.Clear(enumerators);
            Array.Clear(awaiters);

            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
            
            yield break;
            
            void OnCompleted(int index)
            {
                channel.Writer.TryWrite(index);
            }

            void MoveNext(int index)
            {
                var enumerator = enumerators[index];
                var awaiter = enumerator.MoveNextAsync().GetAwaiter();
                
                awaiter.OnCompleted(() => OnCompleted(index));

                awaiters[index] = awaiter;
            }
        }
        
    }
}