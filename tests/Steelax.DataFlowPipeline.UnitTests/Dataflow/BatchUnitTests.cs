using System.Collections;
using Steelax.DataFlowPipeline.UnitTests.Mocks;

namespace Steelax.DataFlowPipeline.UnitTests.Dataflow;

public sealed class BatchUnitTests
{
    public sealed class TestItems : IEnumerable<object[]>
    {
        private readonly List<object[]> _items =
        [
            new object[]
            {
                new[] { 1, 2, 3, 4, 5 },
                2,
                TimeSpan.Zero,
                TimeSpan.FromDays(1),
                //Timeout.InfiniteTimeSpan,
                new int[][] { [1, 2], [3, 4], [5] }
            }
        ];

        public IEnumerator<object[]> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    
    [Theory]
    [ClassData(typeof(TestItems))]
    public async Task Batch_Success(
        int[] items,
        int batchSize,
        TimeSpan delay,
        TimeSpan timeout,
        int[][] expected)
    {
        var dataflow = new DataflowChannelWriter<Batch<int>>();

        await items
            .ToAsyncEnumerable()
            .WhereAwait(async s =>
            {
                await Task.Delay(delay);
                return true;
            })
            .UseAsDataflowSource()
            .Batch(batchSize, timeout)
            .EndWith(dataflow)
            .InvokeAsync(CancellationToken.None);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(expected, real.Select(s => s.Span.ToArray()));
    }
}