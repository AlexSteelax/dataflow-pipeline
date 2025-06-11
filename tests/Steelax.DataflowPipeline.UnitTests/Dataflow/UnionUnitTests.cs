using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class UnionUnitTests
{
    [Fact]
    public async Task Union_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Union(actual.ToAsyncEnumerable().UseAsDataflowSource())
            .EndWith(dataflow)
            .InvokeAsync(CancellationToken.None);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual.Sum() * 2, real.Sum());
    }
}
