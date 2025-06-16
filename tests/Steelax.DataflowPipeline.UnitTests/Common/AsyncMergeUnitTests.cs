using System.Runtime.CompilerServices;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline.UnitTests.Common;

public sealed class AsyncMergeUnitTests
{
    #region Helpers
    
    private static async IAsyncEnumerable<int> CreateAsync(int value, TimeSpan timespan, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(timespan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        yield return value;
    }
    
    private static async IAsyncEnumerable<int> CreateFailAsync(TimeSpan timespan)
    {
        await Task.Delay(timespan);
        throw new Exception(nameof(CreateFailAsync));
        
        // ReSharper disable once HeuristicUnreachableCode
        #pragma warning disable CS0162 // Unreachable code detected
        yield break;
        #pragma warning restore CS0162 // Unreachable code detected
    }
    
    #endregion
    
    [Fact]
    public async Task AsyncMerge_Success()
    {
        var tasks = new[]
        {
            CreateAsync(1, TimeSpan.FromMilliseconds(100)),
            CreateAsync(2, TimeSpan.FromMilliseconds(50)),
            CreateAsync(3, TimeSpan.FromMilliseconds(10))
        };

        var ret = await tasks.MergeAsync().ToArrayAsync();
        
        Assert.NotEqual([1,2,3], ret);
    }
    
    [Fact]
    public async Task AsyncMerge_Exception_Success()
    {
        var tasks = new[]
        {
            CreateAsync(1, TimeSpan.FromMilliseconds(100)),
            CreateFailAsync(TimeSpan.FromMilliseconds(50)),
            CreateAsync(3, TimeSpan.FromMilliseconds(10))
        };

        var task = tasks.MergeAsync().ToArrayAsync().AsTask();

        await Assert.ThrowsAsync<AggregateException>(() => task);

        try
        {
            await task;
        }
        catch(AggregateException ex)
        {
            Assert.Single(ex.InnerExceptions);
            Assert.Contains(ex.InnerExceptions, s => s.Message == nameof(CreateFailAsync));
        }
    }
    
    [Fact]
    public async Task AsyncMerge_Order_Success()
    {
        var tasks = new[]
        {
            CreateAsync(1, TimeSpan.FromMilliseconds(100)),
            CreateAsync(2, TimeSpan.FromMilliseconds(50)),
            CreateAsync(3, TimeSpan.FromMilliseconds(10))
        };

        var ret = await AsyncEnumerableEx.Merge(tasks).ToArrayAsync();
        
        Assert.NotEqual([1,2,3], ret);
    }
}