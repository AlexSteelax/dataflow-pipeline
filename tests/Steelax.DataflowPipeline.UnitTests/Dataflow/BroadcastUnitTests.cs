using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class BroadcastUnitTests
{
    [Theory]
    [InlineData(10, 1)]
    [InlineData(10, 2)]
    public async Task Broadcast_Success(int itemCount, int nextCount)
    {
        const int value = 1;
        var block = new DataflowChannelWriter<int>();
        var source = Enumerable.Range(value, itemCount).ToAsyncEnumerable();

        await new DataflowTask<int>(_ => source)
            .Broadcast(Enumerable
                .Range(0, nextCount)
                .Select(_ => (Func<DataflowTask<int>, DataflowTask>)Next)
                .ToArray())
            .InvokeAsync(CancellationToken.None);

        var expected = Enumerable.Range(value, itemCount).Sum() * nextCount;
        var real = await block.ReadAllAsync();
        
        Assert.Equal(expected, real.Sum());

        return;
        
        DataflowTask Next(DataflowTask<int> input) => input.EndWith(block);
    }
}