using System.Collections;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataFlowPipeline.UnitTests.Common;

public sealed class WaitTimeoutUnitTests
{
    public sealed class TestItems : IEnumerable<object[]>
    {
        private readonly List<object[]> _items =
        [
            new object[]
            {
                new[] { 1, 2, 3 },
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(90),
                true,
                new[] { -1, 1, -1, 2, -1, 3 }
            },
            new object[]
            {
                new[] { 1, 2 },
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(130),
                false,
                new[] { 1, -1, 2 }
            }
        ];

        public IEnumerator<object[]> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    
    [Theory]
    [ClassData(typeof(TestItems))]
    public async Task WaitSeq_Success(
        int[] items,
        TimeSpan delay,
        TimeSpan timeout,
        bool reset,
        int[] expected)
    {
        var source = items
            .ToAsyncEnumerable()
            .TakeWhileAwait(async _ =>
            {
                await Task.Delay(delay);
                return true;
            });

        var ret = await source.WaitTimeoutAsync(timeout, reset, CancellationToken.None).ToListAsync();
        
        Assert.Equal(expected, ret.Select(s => s.Expired ? -1 : s.Value));
    }
}