using System.Buffers;
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
                new int[][] { [1, 2], [3, 4], [5] }
            },
            new object[]
            {
                new[] { 1, 2 },
                2,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(50),
                new int[][] { [1, 2] }
            },
            new object[]
            {
                new[] { 1, 2, 3 },
                1,
                TimeSpan.Zero,
                TimeSpan.FromDays(1),
                new int[][] { [1], [2], [3] }
            },
            new object[]
            {
                new[] { 1, 2, 3 },
                1,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(90),
                new int[][] { [], [1], [], [2], [], [3] }
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
        var dataflow = new DataflowChannelWriter<IMemoryOwner<int>>();

        await items
            .ToAsyncEnumerable()
            .TakeWhileAwait(async s =>
            {
                await Task.Delay(delay);
                return true;
            })
            .UseAsDataflowSource()
            .Batch(batchSize, timeout)
            .EndWith(dataflow)
            .InvokeAsync(CancellationToken.None);

        var real = await dataflow.ReadAllAsync();
        
        Assert.Equal(expected, real.Select(s => s.Memory.ToArray()));
    }
}