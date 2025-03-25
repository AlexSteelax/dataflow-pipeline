using System.Numerics;
using Steelax.DataflowPipeline.DefaultBlocks;

namespace Steelax.DataFlowPipeline.UnitTests;

public class BlockUnitTests
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
    
    //[Theory(Skip = "Not implemented yet")]
    // [Theory]
    // [InlineData(100)]
    // public async Task TimeoutInterrupter_NoTimeout_Success(short size)
    // {
    //     var block = new DataflowPeriodic<short>(Timeout.InfiniteTimeSpan);
    //     var values = CreateSequence(size, TimeSpan.FromMilliseconds(10));
    //
    //     var result = await block.HandleAsync(values, CancellationToken.None).ToListAsync();
    //     
    //     Assert.NotEmpty(result);
    //     Assert.Equal(size, result.Count);
    // }
    
    //[Theory(Skip = "Not implemented yet")]
    // [Theory]
    // [InlineData(5)]
    // public async Task TimeoutInterrupter_Timeout_Success(short size)
    // {
    //     var block = new DataflowPeriodic<short>(TimeSpan.FromMilliseconds(5));
    //     var values = CreateSequence(size, TimeSpan.FromMilliseconds(10));
    //
    //     var result = await block.HandleAsync(values, CancellationToken.None).ToListAsync();
    //     
    //     Assert.NotEmpty(result);
    //     Assert.Equal(size, result.Count);
    // }
}