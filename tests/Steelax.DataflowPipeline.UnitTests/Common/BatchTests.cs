namespace Steelax.DataflowPipeline.UnitTests.Common;

public class BatchTests
{
    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 1, 2, 3, 4, 5 })]
    public void From_WithValidData_CreatesBatchWithCorrectContent(int[] source)
    {
        // Act
        using var batch = Batch<int>.From(source);

        // Assert
        Assert.Equal(source.Length, batch.Count);
        Assert.Equal(source, batch);
        Assert.Equal(source.Length == 0, batch.IsEmpty);
    }

    [Fact]
    public void From_WithEmptyBuffer_ReturnsEmptySingleton()
    {
        // Arrange
        var emptyBuffer = Array.Empty<int>();

        // Act
        using var batch = Batch<int>.From(emptyBuffer);

        // Assert
        Assert.Same(Batch<int>.Empty, batch);
        Assert.True(batch.IsEmpty);
        Assert.Empty(batch);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Indexer_WithValidIndex_ReturnsCorrectElement(int index)
    {
        // Arrange
        var source = new[] { 10, 20, 30 };
        using var batch = Batch<int>.From(source);

        // Act
        var element = batch[index];

        // Assert
        Assert.Equal(source[index], element);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(100)]
    public void Indexer_WithInvalidIndex_ThrowsArgumentOutOfRangeException(int invalidIndex)
    {
        // Arrange
        using var batch = Batch<int>.From([1, 2, 3]);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => batch[invalidIndex]);
    }

    [Fact]
    public void Indexer_OnDisposedBatch_ThrowsObjectDisposedException()
    {
        // Arrange
        var batch = Batch<int>.From([1, 2, 3]);
        batch.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _ = batch[0]);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 1, 2, 3 })]
    public void GetEnumerator_EnumeratesAllElements(int[] source)
    {
        // Arrange
        using var batch = Batch<int>.From(source);

        // Act
        var enumerated = batch.ToList();

        // Assert
        Assert.Equal(source, enumerated);
    }

    [Fact]
    public void GetEnumerator_OnDisposedBatch_ThrowsObjectDisposedException()
    {
        // Arrange
        var batch = Batch<int>.From([1, 2, 3]);
        batch.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() =>
        {
            using var enumerator = batch.GetEnumerator();
            return enumerator.MoveNext();
        });
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 1, 2, 3 })]
    public void AsSpan_ReturnsCorrectSpan(int[] source)
    {
        // Arrange
        using var batch = Batch<int>.From(source);

        // Act
        var span = batch.AsSpan();

        // Assert
        Assert.Equal(source.Length, span.Length);
        Assert.True(span.SequenceEqual(source));
    }

    [Fact]
    public void AsSpan_OnDisposedBatch_ThrowsObjectDisposedException()
    {
        // Arrange
        var batch = Batch<int>.From([1, 2, 3]);
        batch.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => batch.AsSpan());
    }

    [Theory]
    [InlineData(new int[0], true)]
    [InlineData(new[] { 1 }, false)]
    [InlineData(new[] { 1, 2, 3 }, false)]
    public void IsEmpty_ReturnsCorrectValue(int[] source, bool expectedIsEmpty)
    {
        // Arrange
        using var batch = Batch<int>.From(source);

        // Act
        var isEmpty = batch.IsEmpty;

        // Assert
        Assert.Equal(expectedIsEmpty, isEmpty);
    }

    [Fact]
    public void IsEmpty_OnDisposedBatch_ThrowsObjectDisposedException()
    {
        // Arrange
        var batch = Batch<int>.From([1]);
        batch.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _ = batch.IsEmpty);
    }

    [Fact]
    public void Count_OnDisposedBatch_ThrowsObjectDisposedException()
    {
        // Arrange
        var batch = Batch<int>.From([1, 2, 3]);
        batch.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _ = batch.Count);
    }

    [Fact]
    public void Empty_IsSingleton()
    {
        // Act
        var empty1 = Batch<int>.Empty;
        var empty2 = Batch<int>.Empty;

        // Assert
        Assert.Same(empty1, empty2);
    }

    [Fact]
    public void Empty_ShouldNotBeDisposed()
    {
        // Arrange
        var empty = Batch<int>.Empty;

        // Act & Assert - Should not throw on access
        _ = empty.IsEmpty;
        _ = empty.Count;
        Assert.Empty(empty);
    }

    [Fact]
    public void Batch_WithStrings_WorksCorrectly()
    {
        // Arrange & Act
        var source = new[] { "a", "b", "c" };
        using var batch = Batch<string>.From(source);

        // Assert
        Assert.Equal(source.Length, batch.Count);
        Assert.Equal(source, batch);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimesSafely()
    {
        // Arrange
        var batch = Batch<int>.From([1, 2, 3]);

        // Act & Assert - Should not throw
        batch.Dispose();
        batch.Dispose();
        batch.Dispose();
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 })]
    public void Dispose_RendersBatchUnusable(int[] source)
    {
        // Arrange
        var batch = Batch<int>.From(source);

        // Act
        batch.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => _ = batch.Count);
        Assert.Throws<ObjectDisposedException>(() => _ = batch.IsEmpty);
        Assert.Throws<ObjectDisposedException>(() => _ = batch[0]);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            using var enumerator = batch.GetEnumerator();
            return enumerator.MoveNext();
        });
        Assert.Throws<ObjectDisposedException>(() => batch.AsSpan());
    }

    [Fact]
    public void Batch_FromSpan_WorksCorrectly()
    {
        // Arrange
        var source = new[] { 10, 20, 30 };
        ReadOnlySpan<int> span = source;

        // Act
        using var batch = Batch<int>.From(span);

        // Assert
        Assert.Equal(3, batch.Count);
        Assert.Equal(10, batch[0]);
        Assert.Equal(20, batch[1]);
        Assert.Equal(30, batch[2]);
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 1, 2, 3, 4, 5 })]
    public void IEnumerable_GetEnumerator_ReturnsSameEnumerator(int[] source)
    {
        // Arrange
        using var batch = Batch<int>.From(source);
        using var genericEnumerator = batch.GetEnumerator();
        // ReSharper disable once GenericEnumeratorNotDisposed
        var nonGenericEnumerator = ((System.Collections.IEnumerable)batch).GetEnumerator();

        // Act & Assert - Both enumerators should produce the same sequence
        while (genericEnumerator.MoveNext())
        {
            Assert.True(nonGenericEnumerator.MoveNext());
            Assert.Equal(genericEnumerator.Current, nonGenericEnumerator.Current);
        }

        Assert.False(nonGenericEnumerator.MoveNext());
    }
}