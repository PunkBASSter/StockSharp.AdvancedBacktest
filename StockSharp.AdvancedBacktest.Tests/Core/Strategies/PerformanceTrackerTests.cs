using Microsoft.Extensions.Logging.Abstractions;
using StockSharp.AdvancedBacktest.Core.Strategies;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies;

/// <summary>
/// Unit tests for PerformanceTracker implementation
/// </summary>
public class PerformanceTrackerTests
{
    private readonly PerformanceTracker _performanceTracker;

    public PerformanceTrackerTests()
    {
        _performanceTracker = new PerformanceTracker(NullLogger<PerformanceTracker>.Instance);
    }

    [Fact]
    public void PerformanceTracker_InitialState_ShouldBeValid()
    {
        // Assert
        Assert.Equal(100_000m, _performanceTracker.CurrentValue);
        Assert.Equal(0m, _performanceTracker.TotalReturn);
        Assert.Equal(0m, _performanceTracker.SharpeRatio);
        Assert.Equal(0m, _performanceTracker.MaxDrawdown);
        Assert.Equal(0m, _performanceTracker.CurrentDrawdown);
        Assert.Equal(0m, _performanceTracker.WinRate);
        Assert.Equal(0, _performanceTracker.TotalTrades);
        Assert.Equal(0, _performanceTracker.WinningTrades);
        Assert.True(_performanceTracker.IsConsistent);
    }

    [Fact]
    public void PerformanceTracker_UpdatePortfolioValue_ShouldCalculateReturns()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        _performanceTracker.UpdatePortfolioValue(110_000m, timestamp);

        // Assert
        Assert.Equal(110_000m, _performanceTracker.CurrentValue);
        Assert.Equal(0.1m, _performanceTracker.TotalReturn); // 10% return
        Assert.Equal(0m, _performanceTracker.CurrentDrawdown); // No drawdown yet
    }

    [Fact]
    public void PerformanceTracker_UpdatePortfolioValue_WithDrawdown_ShouldCalculateCorrectly()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        _performanceTracker.UpdatePortfolioValue(120_000m, timestamp); // Peak at 120k
        _performanceTracker.UpdatePortfolioValue(108_000m, timestamp.AddHours(1)); // Draw down to 108k

        // Assert
        Assert.Equal(108_000m, _performanceTracker.CurrentValue);
        Assert.Equal(0.08m, _performanceTracker.TotalReturn); // 8% total return
        Assert.Equal(0.1m, _performanceTracker.CurrentDrawdown); // 10% drawdown from peak
        Assert.Equal(0.1m, _performanceTracker.MaxDrawdown); // 10% max drawdown
    }

    [Fact]
    public void PerformanceTracker_RecordTrade_ShouldUpdateTradeStatistics()
    {
        // Arrange
        var security = new Security { Code = "AAPL" };
        var winningTrade = new Trade
        {
            Id = 1,
            Security = security,
            Price = 150m,
            Volume = 100,
            Time = DateTimeOffset.UtcNow
        };
        var losingTrade = new Trade
        {
            Id = 2,
            Security = security,
            Price = 149m,
            Volume = 100,
            Time = DateTimeOffset.UtcNow.AddMinutes(1),
        };

        // Act
        _performanceTracker.RecordTrade(winningTrade);
        _performanceTracker.RecordTrade(losingTrade);

        // Assert
        Assert.Equal(2, _performanceTracker.TotalTrades);
        Assert.Equal(1, _performanceTracker.WinningTrades);
        Assert.Equal(0.5m, _performanceTracker.WinRate); // 50% win rate
    }

    [Fact]
    public void PerformanceTracker_CalculateVolatility_WithInsufficientData_ShouldReturnZero()
    {
        // Act
        var volatility = _performanceTracker.CalculateVolatility();

        // Assert
        Assert.Equal(0m, volatility);
    }

    [Fact]
    public void PerformanceTracker_GetSnapshot_ShouldReturnCurrentMetrics()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        _performanceTracker.UpdatePortfolioValue(110_000m, timestamp);

        // Act
        var snapshot = _performanceTracker.GetSnapshot();

        // Assert
        Assert.Equal(110_000m, snapshot.PortfolioValue);
        Assert.Equal(0.1m, snapshot.TotalReturn);
        Assert.Equal(0m, snapshot.CurrentDrawdown);
        Assert.Equal(0, snapshot.TotalTrades);
    }

    [Fact]
    public void PerformanceTracker_GetHistory_ShouldReturnHistoricalSnapshots()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        _performanceTracker.UpdatePortfolioValue(105_000m, baseTime);
        _performanceTracker.UpdatePortfolioValue(110_000m, baseTime.AddHours(1));
        _performanceTracker.UpdatePortfolioValue(115_000m, baseTime.AddHours(2));

        // Act
        var history = _performanceTracker.GetHistory();

        // Assert
        Assert.Equal(3, history.Length);
        Assert.Equal(105_000m, history[0].PortfolioValue);
        Assert.Equal(110_000m, history[1].PortfolioValue);
        Assert.Equal(115_000m, history[2].PortfolioValue);
    }

    [Fact]
    public void PerformanceTracker_GetHistory_WithDateRange_ShouldFilterCorrectly()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        _performanceTracker.UpdatePortfolioValue(105_000m, baseTime);
        _performanceTracker.UpdatePortfolioValue(110_000m, baseTime.AddHours(1));
        _performanceTracker.UpdatePortfolioValue(115_000m, baseTime.AddHours(2));
        _performanceTracker.UpdatePortfolioValue(120_000m, baseTime.AddHours(3));

        // Act
        var history = _performanceTracker.GetHistory(baseTime.AddHours(1), baseTime.AddHours(2));

        // Assert
        Assert.Equal(2, history.Length);
        Assert.Equal(110_000m, history[0].PortfolioValue);
        Assert.Equal(115_000m, history[1].PortfolioValue);
    }

    [Fact]
    public void PerformanceTracker_Reset_ShouldClearAllData()
    {
        // Arrange
        _performanceTracker.UpdatePortfolioValue(110_000m, DateTimeOffset.UtcNow);
        var trade = new Trade
        {
            Id = 1,
            Security = new Security { Code = "AAPL" },
        };
        _performanceTracker.RecordTrade(trade);

        // Act
        _performanceTracker.Reset();

        // Assert
        Assert.Equal(100_000m, _performanceTracker.CurrentValue);
        Assert.Equal(0m, _performanceTracker.TotalReturn);
        Assert.Equal(0, _performanceTracker.TotalTrades);
        Assert.Equal(0, _performanceTracker.WinningTrades);
        Assert.True(_performanceTracker.GetHistory().IsEmpty);
    }

    [Fact]
    public void PerformanceTracker_Dispose_ShouldCleanUpResources()
    {
        // Arrange
        _performanceTracker.UpdatePortfolioValue(110_000m, DateTimeOffset.UtcNow);

        // Act
        _performanceTracker.Dispose();

        // Assert - Should not throw and metrics should still be accessible
        Assert.Equal(110_000m, _performanceTracker.CurrentValue);
    }

    [Theory]
    [InlineData(100_000, 110_000, 0.1)] // 10% gain
    [InlineData(100_000, 90_000, -0.1)] // 10% loss
    [InlineData(100_000, 100_000, 0.0)] // No change
    public void PerformanceTracker_TotalReturn_ShouldCalculateCorrectly(decimal initial, decimal current, decimal expectedReturn)
    {
        // Arrange
        var tracker = new PerformanceTracker(NullLogger<PerformanceTracker>.Instance);

        // Act
        tracker.UpdatePortfolioValue(current, DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(expectedReturn, tracker.TotalReturn, 4); // 4 decimal places precision
    }
}

/// <summary>
/// Performance tests for PerformanceTracker
/// </summary>
public class PerformanceTrackerPerformanceTests
{
    [Fact]
    public void PerformanceTracker_HighFrequencyUpdates_ShouldPerformWell()
    {
        // Arrange
        var tracker = new PerformanceTracker(NullLogger<PerformanceTracker>.Instance);
        const int updateCount = 10_000;
        var startTime = DateTimeOffset.UtcNow;

        // Act
        var start = DateTime.UtcNow;
        for (int i = 0; i < updateCount; i++)
        {
            tracker.UpdatePortfolioValue(100_000m + i, startTime.AddSeconds(i));
        }
        var elapsed = DateTime.UtcNow - start;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 1000, $"Performance test failed: {elapsed.TotalMilliseconds}ms for {updateCount} updates");
        Assert.Equal(100_000m + updateCount - 1, tracker.CurrentValue);
    }

    [Fact]
    public void PerformanceTracker_HighFrequencyTradeRecording_ShouldPerformWell()
    {
        // Arrange
        var tracker = new PerformanceTracker(NullLogger<PerformanceTracker>.Instance);
        const int tradeCount = 10_000;
        var security = new Security { Code = "TEST" };

        // Act
        var start = DateTime.UtcNow;
        for (int i = 0; i < tradeCount; i++)
        {
            var trade = new Trade
            {
                Id = i,
                Security = security,
                Price = 100m + (i % 20),
                Volume = 100,
                Time = DateTimeOffset.UtcNow.AddSeconds(i),
            };
            tracker.RecordTrade(trade);
        }
        var elapsed = DateTime.UtcNow - start;

        // Assert
        Assert.True(elapsed.TotalMilliseconds < 1000, $"Trade recording performance test failed: {elapsed.TotalMilliseconds}ms for {tradeCount} trades");
        Assert.Equal(tradeCount, tracker.TotalTrades);
        Assert.Equal(tradeCount / 2, tracker.WinningTrades); // Half should be winning
    }
}