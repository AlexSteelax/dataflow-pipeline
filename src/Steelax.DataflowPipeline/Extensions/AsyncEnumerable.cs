using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Steelax.DataflowPipeline.Common;

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

    public static async IAsyncEnumerable<TimedResult<TValue>> WaitTimeoutAsync<TValue>(
        this IAsyncEnumerable<TValue> source,
        TimeSpan interval,
        bool reset = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var state = new State();
        
        await using var timer = new Timer(static state => ((State)state!).Change(OperationState.Timeout), state, interval, interval);

        ValueTask<bool> nextResultTask = default;

        while (!cancellationToken.IsCancellationRequested)
        {
            switch ((OperationState)state)
            {
                case OperationState.Next:
                    state.Change(OperationState.Waiting);
                    
#pragma warning disable CA2012
                    nextResultTask = enumerator.MoveNextAsync();
#pragma warning restore CA2012
                    nextResultTask.GetAwaiter().OnCompleted(() => state.Change(OperationState.ResultCompleted));

                    if (reset)
                        timer.Change(interval, interval);
                    break;
                case OperationState.Waiting:
                    await Task.Yield();
                    break;
                case OperationState.ResultCompleted:
                    if (nextResultTask.Result)
                    {
                        var result = enumerator.Current;
                        state.Change(OperationState.Next);
                        yield return new TimedResult<TValue>(result);
                    }
                    else
                    {
                        yield break;
                    }
                    break;
                case OperationState.Timeout:
                    state.Change(OperationState.Waiting);
                    yield return new TimedResult<TValue>();
                    break;
                default:
#pragma warning disable CA2208
                    throw new ArgumentOutOfRangeException();
#pragma warning restore CA2208
            }
        }
    }
    
    private sealed class State
    {
        private int _state;
        private SpinLock _lock;

        private OperationState OperationState => (OperationState)_state;
        
        public void Change(OperationState newState)
        {
            var lockTaken = false;
            
            try
            {
                _lock.Enter(ref lockTaken);

                var update = newState switch
                {
                    OperationState.Waiting when OperationState is OperationState.Next => true,
                    OperationState.Timeout when OperationState is OperationState.Waiting => true,
                    OperationState.ResultCompleted when OperationState is OperationState.Waiting or OperationState.Timeout => true,
                    OperationState.Next when OperationState is OperationState.ResultCompleted => true,
                    _ => false
                };
                
                if (update)
                    _state = (int)newState;
            }
            finally
            {
                if (lockTaken)
                    _lock.Exit(false);
            }
        }
        
        public static implicit operator OperationState(State value) => (OperationState)value._state;
    }

    private enum OperationState
    {
        Next = 0,
        Waiting = 1,
        ResultCompleted = 2,
        Timeout = 3
    }
}