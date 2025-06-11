using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class BroadcastUnitTests
{
    [Theory]
    [InlineData(10, 1)]
    public async Task Broadcast_Success(int itemCount, int nextCount)
    {
        const int value = 1;
        var dataflow = new DataflowChannelWriter<int>();

        await Enumerable.Range(value, itemCount)
            .ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Broadcast(Enumerable
                .Range(0, nextCount)
                .Select(_ => (Func<DataflowTask<int>, DataflowTask>)Next)
                .ToArray())
            .InvokeAsync(CancellationToken.None);

        var expected = Enumerable.Range(value, itemCount).Sum() * nextCount;
        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(expected, real.Sum());

        return;
        
        DataflowTask Next(DataflowTask<int> input) => input.EndWith(dataflow);
    }
    
    [Theory]
    [InlineData(10, 2)]
    public async Task BroadcastContinue_Success(int itemCount, int nextCount)
    {
        const int value = 1;
        var dataflow = new DataflowChannelWriter<int>();

        await Enumerable.Range(value, itemCount)
            .ToAsyncEnumerable()
            .UseAsDataflowSource()
            .BroadcastContinue(Enumerable
                .Range(0, nextCount)
                .Select(_ => (Func<DataflowTask<int>, DataflowTask>)Next)
                .ToArray())
            .EndWith(dataflow)
            .InvokeAsync(CancellationToken.None);

        var expected = Enumerable.Range(value, itemCount).Sum();
        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(expected, real.Sum());

        return;
        
        DataflowTask Next(DataflowTask<int> input) => input.End();
    }
}