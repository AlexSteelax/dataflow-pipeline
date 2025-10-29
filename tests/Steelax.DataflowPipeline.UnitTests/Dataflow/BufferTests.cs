using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class BufferTests
{
    [Fact]
    public async Task Buffer_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var block = new DataflowChannelWriter<int>();
        var source = actual.ToAsyncEnumerable();

        await new DataflowTask<int>(_ => source)
            .Buffer(2)
            .EndWith(block)
            .InvokeAsync(CancellationToken.None);

        Assert.Equal(actual, block.ReadAll(TestContext.Current.CancellationToken));
    }
}