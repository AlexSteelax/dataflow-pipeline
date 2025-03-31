using System.Numerics;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataFlowPipeline.UnitTests.Dataflow;

public sealed class PeriodicUnitTests
{
    private static async IAsyncEnumerable<T> CreateSequence<T>(T size, TimeSpan delay)
        where T : struct, INumber<T>
    {
        for (var i = T.Zero; i < size; i++)
        {
            await Task.Delay(delay);
            yield return i;
        }
    }
    
    [Theory]
    [InlineData(100)]
    public async Task Periodic_NoTimeout_Success(int size)
    {
        var block = new DataflowPeriodic<int>(TimeSpan.FromDays(1), true);
        var expected = Enumerable.Range(0, size).ToList();
        
        var result = await block.HandleAsync(expected.ToAsyncEnumerable(), CancellationToken.None).ToListAsync();
        
        Assert.NotEmpty(result);
        Assert.DoesNotContain(result, s => s.Expired);
        Assert.Equivalent(expected, result.Select(s => s.Value));
    }
    
    [Theory]
    [InlineData(5)]
    public async Task Periodic_Success(int size)
    {
        var block = new DataflowPeriodic<int>(TimeSpan.FromMilliseconds(5), true);
        var values = CreateSequence(size, TimeSpan.FromMilliseconds(10));
        var expected = Enumerable.Range(0, size).ToList();
    
        var result = await block.HandleAsync(values, CancellationToken.None).ToListAsync();
        
        Assert.NotEmpty(result);
        Assert.Contains(result, s => s.Expired);
        Assert.Contains(result, s => !s.Expired);
        Assert.Equivalent(expected, result.Where(s => !s.Expired).Select(s => s.Value));
    }
}