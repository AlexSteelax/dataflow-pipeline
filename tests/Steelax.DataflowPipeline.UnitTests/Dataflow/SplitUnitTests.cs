using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class SplitUnitTests
{
    [Fact]
    public async Task Split_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow1 = new DataflowChannelWriter<int>();
        var dataflow2 = new DataflowChannelWriter<int>();

        var source = actual.ToAsyncEnumerable();

        await new DataflowTask<int>(_ => source)
            .Split((v, i) => v % 2, [
                (i => i == 1, s => s.EndWith(dataflow1)),
                (i => i == 0, s => s.EndWith(dataflow2))])
            .InvokeAsync(CancellationToken.None);

        Assert.Equal([1,3,5], dataflow1.ReadAll());
        Assert.Equal([2,4], dataflow2.ReadAll());
    }
    
    [Fact]
    public async Task SplitRound_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow1 = new DataflowChannelWriter<int>();
        var dataflow2 = new DataflowChannelWriter<int>();
        
        var source = actual.ToAsyncEnumerable();

        await new DataflowTask<int>(_ => source)
            .Split([
                s => s.EndWith(dataflow1),
                s => s.EndWith(dataflow2)])
            .InvokeAsync(CancellationToken.None);

        Assert.Equal([1,3,5], dataflow1.ReadAll());
        Assert.Equal([2,4], dataflow2.ReadAll());
    }
}