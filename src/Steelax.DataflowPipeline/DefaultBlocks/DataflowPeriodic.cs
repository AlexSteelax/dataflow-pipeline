using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Abstractions;
using Steelax.DataflowPipeline.Common;
using Timer = System.Threading.Timer;

namespace Steelax.DataflowPipeline.DefaultBlocks;

internal class DataflowPeriodic<TValue>(TimeSpan period, bool reset)
    : IDataflowTransform<TValue, TimedResult<TValue>>
{
    public async IAsyncEnumerable<TimedResult<TValue>> HandleAsync(IAsyncEnumerable<TValue> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        var state = new State();
        
        await using var timer = new Timer(static state => ((State)state!).Change(OperationState.Timeout), state, period, period);

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
                        timer.Change(period, period);
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