using Steelax.DataflowPipeline.Common;

namespace Steelax.DataFlowPipeline.UnitTests.Common;

public class UnitTestPacker
{
    [Fact]
    public void TestAdd()
    {
        const int size = 3;
        var buffer = new Packer<int>(size);
        var items = Enumerable.Range(0, size).ToArray();

        Assert.True(buffer.IsEmpty);
        
        foreach (var item in items.SkipLast(1))
            Assert.False(buffer.TryAddAndGet(item, out _));
        
        Assert.True(buffer.TryAddAndGet(items.Last(), out var package));

        Assert.NotNull(package);
        Assert.NotEmpty(package);

        Assert.Equivalent(items, package);
    }

    [Fact]
    public void TestAdd_SingleSize()
    {
        var buffer = new Packer<int>(1);

        Assert.True(buffer.TryAddAndGet(1, out var package));
        Assert.Equivalent(new[] { 1 }, package);
        Assert.True(buffer.IsEmpty);

        Assert.True(buffer.TryAddAndGet(2, out package));
        Assert.Equivalent(new[] { 2 }, package);
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void TestAdd_ManySize()
    {
        var buffer = new Packer<int>(2);

        Assert.False(buffer.TryAddAndGet(1, out var ret));
        Assert.True(buffer.TryAddAndGet(2, out ret));
        Assert.Equivalent(new[] { 1, 2 }, ret);
        Assert.True(buffer.IsEmpty);
    }
}