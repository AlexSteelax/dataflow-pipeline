using System.Buffers;
using System.Collections;
using Steelax.DataflowPipeline.UnitTests.Mocks;

namespace Steelax.DataflowPipeline.UnitTests.Dataflow;

public sealed class BatchTests
{
    public sealed class TestItems : IEnumerable<object[]>
    {
        private readonly List<object[]> _items =
        [
            [
                new[] { 1, 2, 3, 4, 5 },
                2,
                TimeSpan.Zero,
                TimeSpan.FromDays(1),
                new int[][] { [1, 2], [3, 4], [5] }
            ],
            [
                new[] { 1, 2 },
                2,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(50),
                new int[][] { [1, 2] }
            ],
            [
                new[] { 1, 2, 3 },
                1,
                TimeSpan.Zero,
                TimeSpan.FromDays(1),
                new int[][] { [1], [2], [3] }
            ],
            [
                new[] { 1, 2, 3 },
                1,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(90),
                new int[][] { [], [1], [], [2], [], [3] }
            ]
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
        var block = new DataflowChannelWriter<Batch<int>>();
        var source = items
            .ToAsyncEnumerable()
            .TakeWhileAwait(async s =>
            {
                await Task.Delay(delay);
                return true;
            });
        
        await new DataflowTask<int>(_ => source)
            .Batch(batchSize, timeout)
            .EndWith(block)
            .InvokeAsync(CancellationToken.None);

        Assert.Equal(expected, block.ReadAll(TestContext.Current.CancellationToken).Select(s => s.ToArray()));
    }
}