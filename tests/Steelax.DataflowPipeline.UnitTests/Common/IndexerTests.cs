namespace Steelax.DataflowPipeline.UnitTests.Common;

public class IndexerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Constructor_ValidCapacity_InitializesAllIndicesActive(int capacity)
    {
        // Arrange & Act
        using var indexer = new Indexer(capacity);
        
        // Assert - Все индексы активны и доступны в правильном порядке
        for (var expected = 0; expected < capacity; expected++)
        {
            var actual = indexer.Forward();
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new Indexer(invalidCapacity));
    }

    [Fact]
    public void Forward_FromInitialPosition_StartsFromZero()
    {
        // Arrange
        using var indexer = new Indexer(5);
        
        // Act
        var result = indexer.Forward();
        
        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Preview_FromInitialPosition_StartsFromLastIndex()
    {
        // Arrange
        using var indexer = new Indexer(5);
        
        // Act
        var result = indexer.Preview();
        
        // Assert
        Assert.Equal(4, result);
    }

    [Fact]
    public void Forward_AfterRemove_SkipsInactiveIndices()
    {
        // Arrange
        using var indexer = new Indexer(5);
        var first = indexer.Forward(); // 0
        indexer.Remove(); // Удаляем 0
        
        // Act
        var second = indexer.Forward(); // Должен пропустить 0 и вернуть 1
        
        // Assert
        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void Preview_AfterRemove_SkipsInactiveIndices()
    {
        // Arrange
        using var indexer = new Indexer(5);
        var last = indexer.Preview(); // 4
        indexer.Remove(); // Удаляем 4
        
        // Act
        var previous = indexer.Preview(); // Должен пропустить 4 и вернуть 3
        
        // Assert
        Assert.Equal(4, last);
        Assert.Equal(3, previous);
    }

    [Fact]
    public void Forward_CyclicBehavior_ReturnsToStartAfterLastIndex()
    {
        // Arrange
        using var indexer = new Indexer(3);
        
        // Act - Проходим полный цикл
        Assert.Equal(0, indexer.Forward()); // 0
        Assert.Equal(1, indexer.Forward()); // 1
        Assert.Equal(2, indexer.Forward()); // 2
        Assert.Equal(0, indexer.Forward()); // 0 (циклически)
    }

    [Fact]
    public void Preview_CyclicBehavior_ReturnsToEndAfterFirstIndex()
    {
        // Arrange
        using var indexer = new Indexer(3);
        
        // Act - Проходим полный цикл назад
        Assert.Equal(2, indexer.Preview()); // 2
        Assert.Equal(1, indexer.Preview()); // 1
        Assert.Equal(0, indexer.Preview()); // 0
        Assert.Equal(2, indexer.Preview()); // 2 (циклически)
    }

    [Fact]
    public void Remove_WithoutActiveCurrent_DoesNothing()
    {
        // Arrange
        using var indexer = new Indexer(3);
        // Current = InitialPosition (-2) - нет активного текущего индекса
        
        // Act
        indexer.Remove();
        
        // Assert - Все индексы остаются активными
        Assert.Equal(0, indexer.Forward());
        Assert.Equal(1, indexer.Forward());
        Assert.Equal(2, indexer.Forward());
    }

    [Fact]
    public void StaticMoveForward_ComputesCorrectCyclicIndex()
    {
        // Arrange
        var capacity = 5;
        
        // Act & Assert
        Assert.Equal(1, Indexer.MoveForward(0, 1, capacity));
        Assert.Equal(2, Indexer.MoveForward(1, 1, capacity));
        Assert.Equal(0, Indexer.MoveForward(4, 1, capacity)); // Зацикливание
        Assert.Equal(1, Indexer.MoveForward(4, 2, capacity)); // Зацикливание на несколько шагов
    }

    [Fact]
    public void StaticMoveBackward_ComputesCorrectCyclicIndex()
    {
        // Arrange
        var capacity = 5;
        
        // Act & Assert
        Assert.Equal(3, Indexer.MoveBackward(4, 1, capacity));
        Assert.Equal(2, Indexer.MoveBackward(3, 1, capacity));
        Assert.Equal(4, Indexer.MoveBackward(0, 1, capacity)); // Зацикливание
        Assert.Equal(3, Indexer.MoveBackward(0, 2, capacity)); // Зацикливание на несколько шагов
    }

    [Fact]
    public void MixedOperations_WithRemove_CreatesQueueBehavior()
    {
        // Arrange
        using var indexer = new Indexer(4);
        
        // Act & Assert - Симуляция очереди с извлечением
        Assert.Equal(0, indexer.Forward()); // Берем первый элемент
        indexer.Remove(); // "Извлекаем" его
        
        Assert.Equal(1, indexer.Forward()); // Берем следующий
        indexer.Remove(); // "Извлекаем" его
        
        Assert.Equal(2, indexer.Forward()); // Берем следующий
        // Не удаляем - оставляем активным
        
        Assert.Equal(3, indexer.Forward()); // Продолжаем обход
        indexer.Remove(); // "Извлекаем"
        
        // Теперь активны только индексы 0, 1, 3 удалены, 2 активен
        Assert.Equal(2, indexer.Forward()); // Должен найти активный 2
    }

    [Fact]
    public void AllIndicesRemoved_ForwardAndPreviewStillWorkCyclically()
    {
        // Arrange
        using var indexer = new Indexer(3);
        
        // Act - Удаляем все индексы по очереди
        indexer.Forward(); // 0
        indexer.Remove();
        indexer.Forward(); // 1
        indexer.Remove();
        indexer.Forward(); // 2
        indexer.Remove();
        
        // Assert - После удаления всех индексов Forward/Preview продолжают работать,
        // но не находят активных элементов и возвращаются к циклическому обходу
        // В текущей реализации они будут возвращать -1 когда нет активных индексов
        Assert.Equal(-1, indexer.Forward());
        Assert.Equal(-1, indexer.Preview());
    }

    [Fact]
    public void Dispose_MultipleCalls_AreSafe()
    {
        // Arrange
        var indexer = new Indexer(3);
        
        // Act & Assert
        indexer.Dispose();
        indexer.Dispose(); // Не должно бросать исключение
    }

    [Fact]
    public void ForwardAndPreview_Interleaved_MaintainCorrectCyclicPosition()
    {
        // Arrange
        using var indexer = new Indexer(4);
        
        // Act & Assert
        Assert.Equal(0, indexer.Forward()); // Current = 0
        Assert.Equal(3, indexer.Preview()); // Current = 3 (циклически от 0)
        Assert.Equal(0, indexer.Forward()); // Current = 0 (циклически от 3)
        Assert.Equal(1, indexer.Forward()); // Current = 1
    }

    [Fact]
    public void Remove_DoesNotAffectCyclicNavigation()
    {
        // Arrange
        using var indexer = new Indexer(5);
        
        // Act
        indexer.Forward(); // 0
        indexer.Remove(); // Удаляем 0
        indexer.Forward(); // 1
        // Не удаляем 1
        
        // Assert - Навигация продолжает работать циклически
        Assert.Equal(2, indexer.Forward()); // 2
        Assert.Equal(3, indexer.Forward()); // 3
        Assert.Equal(4, indexer.Forward()); // 4
        Assert.Equal(1, indexer.Forward()); // 1 (циклически, пропуская удаленный 0)
    }
}