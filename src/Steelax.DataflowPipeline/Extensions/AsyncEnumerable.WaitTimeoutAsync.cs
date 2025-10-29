using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Common;

namespace Steelax.DataflowPipeline.Extensions;

internal static partial class AsyncEnumerable
{
    public static async IAsyncEnumerable<TimedAvailability<TValue>> WaitTimeoutAsync<TValue>(
        this IAsyncEnumerable<TValue> source,
        TimeSpan timeout,
        bool reset = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var state = new State();
        #pragma warning disable CA2012
        var nextResult = ValueTask.FromResult(true);
        // ReSharper disable once TooWideLocalVariableScope
        ValueTaskAwaiter<bool> nextResultAwaiter;
        #pragma warning restore CA2012
        var timerInit = false;

        await using var timer = new Timer(static s => ((State)s!).Change(OperationState.Timeout), state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        while (!cancellationToken.IsCancellationRequested)
        {
            var s = state;
            
            switch ((OperationState)state)
            {
                case OperationState.Next:
                    #pragma warning disable CA2012
                    nextResult = enumerator.MoveNextAsync();
                    #pragma warning restore CA2012
                    nextResultAwaiter = nextResult.GetAwaiter();
                    nextResultAwaiter.OnCompleted(() => s.Change(OperationState.Ready));
                    if (!timerInit)
                    {
                        timer.Change(timeout, reset ? Timeout.InfiniteTimeSpan : timeout);
                        timerInit = true;
                    }
                    else if (reset)
                        timer.Change(timeout, Timeout.InfiniteTimeSpan);
                    state.Change(OperationState.Waiting);
                    break;
                case OperationState.Ready:
                    if (nextResult.IsCompletedSuccessfully)
                    {
                        if (nextResult.Result)
                        {
                            var result = enumerator.Current;
                            state.Change(OperationState.Next);
                            yield return TimedAvailability<TValue>.Available(result, DateTimeOffset.UtcNow);
                        }
                        else
                            yield break;
                    }
                    else
                    {
                        //_ = nextResult.Result;
                        throw new InvalidOperationException();
                    }
                    break;
                case OperationState.Timeout:
                    if (reset)
                        timer.Change(timeout, Timeout.InfiniteTimeSpan);
                    state.Change(OperationState.Waiting);
                    yield return TimedAvailability<TValue>.Timeout(DateTimeOffset.UtcNow);
                    break;
                case OperationState.Waiting:
                    await Task.Yield();
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
        private SpinLock _lock;
        private OperationState _state;

        public void Change(OperationState newState)
        {
            var lockTaken = false;
            
            try
            {
                _lock.Enter(ref lockTaken);

                _state = newState switch
                {
                    OperationState.Next when _state is OperationState.Ready => newState,
                    OperationState.Ready when _state is OperationState.Timeout or OperationState.Waiting or OperationState.Next => newState,
                    OperationState.Timeout when _state is OperationState.Waiting => newState,
                    OperationState.Waiting when _state is OperationState.Timeout or OperationState.Next => newState,
                    _ => _state
                };
            }
            finally
            {
                if (lockTaken)
                    _lock.Exit(false);
            }
        }
        
        public static implicit operator OperationState(State value) => value._state;
    }
    
    private enum OperationState
    {
        Next,
        Ready,
        Waiting,
        Timeout
    }
}