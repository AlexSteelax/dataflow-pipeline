
namespace Steelax.DataflowPipeline.UnitTests.Common;

public class PackerUnitTests
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
        
        Assert.True(buffer.TryAddAndGet(items.Last(), out var batch));

        Assert.False(batch.Memory.IsEmpty);

        Assert.Equal(items, batch.Memory.ToArray());
        
        batch.Dispose();
    }

    [Fact]
    public void TestAdd_SingleSize()
    {
        var buffer = new Packer<int>(1);

        Assert.True(buffer.TryAddAndGet(1, out var batch));
        Assert.Equal(new[] { 1 }, batch.Memory.ToArray());
        Assert.True(buffer.IsEmpty);

        Assert.True(buffer.TryAddAndGet(2, out batch));
        Assert.Equal(new[] { 2 }, batch.Memory.ToArray());
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void TestAdd_ManySize()
    {
        var buffer = new Packer<int>(2);

        Assert.False(buffer.TryAddAndGet(1, out var batch));
        Assert.True(buffer.TryAddAndGet(2, out batch));
        Assert.Equal(new[] { 1, 2 }, batch.Memory.ToArray());
        Assert.True(buffer.IsEmpty);
    }
}