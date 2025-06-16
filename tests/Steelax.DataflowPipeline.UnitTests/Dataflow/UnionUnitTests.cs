using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class UnionUnitTests
{
    [Fact]
    public async Task Union_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var block = new DataflowChannelWriter<int>();

        await new DataflowTask<int>(_ => actual.ToAsyncEnumerable())
            .Union(new DataflowTask<int>(_ => actual.ToAsyncEnumerable()))
            .EndWith(block)
            .InvokeAsync(CancellationToken.None);

        Assert.Equal(actual.Sum() * 2, block.ReadAll().Sum());
    }
}
