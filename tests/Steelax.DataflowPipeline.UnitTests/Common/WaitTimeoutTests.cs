using System.Collections;
using Steelax.DataflowPipeline.Extensions;

namespace Steelax.DataflowPipeline.UnitTests.Common;

public sealed class WaitTimeoutTests
{
    public sealed class TestItems : IEnumerable<object[]>
    {
        private readonly List<object[]> _items =
        [
            [
                new[] { 1, 2, 3 },
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(90),
                true,
                new[] { -1, 1, -1, 2, -1, 3 }
            ],
            [
                new[] { 1, 2 },
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(130),
                false,
                new[] { 1, -1, 2 }
            ]
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

        var ret = await source.WaitTimeoutAsync(timeout, reset, CancellationToken.None).ToListAsync(TestContext.Current.CancellationToken);
        
        Assert.Equal(expected, ret.Select(s => s.IsAvailable ? s.Value : -1));
    }
}