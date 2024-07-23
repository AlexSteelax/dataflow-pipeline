using Steelax.DataflowPipeline;

namespace Steelax.DataFlowPipeline.UnitTests.Pipeline;

public class UnitTestDataFlow
{
    [Fact]
    public async Task GreedyPack_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int[]>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Batch(2)
            .EndWith(dataflow)
            .InvokeAsync(default);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual.Chunk(2), real);
    }
    
    [Fact]
    public async Task AggressivePack_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int[]>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Batch(2, TimeSpan.FromMilliseconds(5)).AsUnbounded()
            .EndWith(dataflow)
            .InvokeAsync(default);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual.Chunk(2), real);
    }

    [Fact]
    public async Task Buffer_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Buffer().AsBounded(2)
            .EndWith(dataflow)
            .InvokeAsync(default);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual, real);
    }
    
    [Fact]
    public async Task BroadcastRet_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Broadcast(
                s => s.AsUnbounded(),
                s => s.AsUnbounded())
            .EndWith(dataflow)
            .InvokeAsync(default);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual.Sum() * 2, real.Sum());
    }
    
    [Fact]
    public async Task BroadcastNonRet_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow1 = new DataflowChannelWriter<int>();
        var dataflow2 = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Broadcast(
                s => s.AsUnbounded().EndWith(dataflow1),
                s => s.AsUnbounded().EndWith(dataflow2))
            .InvokeAsync(default);

        var real1 = await dataflow1.ReadAllAsync();
        var real2 = await dataflow2.ReadAllAsync();
        var real = real1.Sum() + real2.Sum();
        
        Assert.Equal(actual.Sum() * 2, real);
    }
    
    [Fact]
    public async Task Split_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow1 = new DataflowChannelWriter<int>();
        var dataflow2 = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Split(
                (_, i) => i % 2,
                s => s.AsUnbounded().EndWith(dataflow1),
                s => s.AsUnbounded().EndWith(dataflow2))
            .InvokeAsync(default);

        var real1 = await dataflow1.ReadAllAsync();
        var real2 = await dataflow2.ReadAllAsync();
        
        Assert.Equal([1,3,5], real1);
        Assert.Equal([2,4], real2);
    }
    
    [Fact]
    public async Task Union_Success()
    {
        int[] actual = [1, 2, 3, 4, 5];

        var dataflow = new DataflowChannelWriter<int>();

        await actual.ToAsyncEnumerable()
            .UseAsDataflowSource()
            .Union(actual.ToAsyncEnumerable().UseAsDataflowSource())
            .EndWith(dataflow)
            .InvokeAsync(default);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(actual.Sum() * 2, real.Sum());
    }
}
