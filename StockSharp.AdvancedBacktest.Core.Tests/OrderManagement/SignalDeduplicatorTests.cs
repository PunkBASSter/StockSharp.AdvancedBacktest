using StockSharp.AdvancedBacktest.OrderManagement;

namespace StockSharp.AdvancedBacktest.Core.Tests.OrderManagement;

public class SignalDeduplicatorTests
{
    [Fact]
    public void IsDuplicate_FirstSignal_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();

        // Act
        var result = deduplicator.IsDuplicate(100m, 95m, 105m);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDuplicate_SameSignalTwice_ReturnsTrue()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);

        // Act
        var result = deduplicator.IsDuplicate(100m, 95m, 105m);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDuplicate_DifferentEntry_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);

        // Act
        var result = deduplicator.IsDuplicate(101m, 95m, 105m);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDuplicate_DifferentStopLoss_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);

        // Act
        var result = deduplicator.IsDuplicate(100m, 94m, 105m);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDuplicate_DifferentTakeProfit_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);

        // Act
        var result = deduplicator.IsDuplicate(100m, 95m, 106m);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Reset_ClearsLastSignal_AllowsSameSignalAgain()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);

        // Act
        deduplicator.Reset();
        var result = deduplicator.IsDuplicate(100m, 95m, 105m);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDuplicate_AfterMultipleSignals_TracksOnlyLast()
    {
        // Arrange
        var deduplicator = new SignalDeduplicator();
        deduplicator.IsDuplicate(100m, 95m, 105m);
        deduplicator.IsDuplicate(110m, 105m, 115m);
        deduplicator.IsDuplicate(120m, 115m, 125m);

        // Act - first signal is no longer tracked, should return false
        var resultFirst = deduplicator.IsDuplicate(100m, 95m, 105m);
        // Last signal is tracked, should return true after re-checking
        var resultLast = deduplicator.IsDuplicate(100m, 95m, 105m);

        // Assert
        Assert.False(resultFirst); // 100/95/105 is new again (was replaced by 110, then 120)
        Assert.True(resultLast);   // Now 100/95/105 is tracked again
    }
}
