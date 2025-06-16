using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Steelax.DataflowPipeline.Extensions;

internal static partial class AsyncEnumerable
{
    public static IAsyncEnumerable<T> MergeAsync<T>(
        this IAsyncEnumerable<T>[] sources,
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
            
            var enumerators = new IAsyncEnumerator<T>[sources.Length];
            
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                enumerators[i] = source.GetAsyncEnumerator(token);
            }

            var moveNextTask = new ValueTask<bool>[sources.Length];

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

            await foreach (var index in channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            {
                var success = false;

                try
                {
                    success = moveNextTask[index].Result;
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
            Array.Clear(moveNextTask);

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
                var task = moveNextTask[index] = enumerator.MoveNextAsync();
                
                task.GetAwaiter().OnCompleted(() => OnCompleted(index));
            }
        }
    }
}