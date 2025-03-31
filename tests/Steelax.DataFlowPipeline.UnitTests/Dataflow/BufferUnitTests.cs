using Steelax.DataFlowPipeline.UnitTests.Mocks;

namespace Steelax.DataFlowPipeline.UnitTests.Dataflow;

public sealed class BufferUnitTests
{
    [Fact]
    public async Task Buffer_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Buffer(2)
            .EndWith(dataflow)
            .InvokeAsync(CancellationToken.None);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual, real);
    }
}