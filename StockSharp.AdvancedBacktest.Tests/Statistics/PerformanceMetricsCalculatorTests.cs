using System.Reflection;
using Ecng.Collections;
using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.Statistics;

public class PerformanceMetricsCalculatorTests
{
    private readonly PerformanceMetricsCalculator _calculator;

    public PerformanceMetricsCalculatorTests()
    {
        _calculator = new PerformanceMetricsCalculator();
    }

    [Fact]
    public void CalculateMetrics_WithNullStrategy_ThrowsArgumentNullException()
    {
        var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        Assert.Throws<ArgumentNullException>(() =>
            _calculator.CalculateMetrics(null!, startDate, endDate));
    }

    [Fact]
    public void CalculateMetrics_WithNoTrades_ReturnsEmptyMetrics()
    {
        var strategy = CreateBasicStrategy();
        var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var metrics = _calculator.CalculateMetrics(strategy, startDate, endDate);

        Assert.Equal(0, metrics.TotalTrades);
        Assert.Equal(0, metrics.TotalReturn);
        Assert.Equal(0, metrics.AnnualizedReturn);
        Assert.Equal(0, metrics.SharpeRatio);
        Assert.Equal(0, metrics.MaxDrawdown);
        Assert.Equal(0, metrics.WinRate);
        Assert.Equal(0, metrics.ProfitFactor);
    }

    [Theory]
    [InlineData(null, 0.02)]      // Default value when not specified
    [InlineData(0.01, 0.01)]
    [InlineData(0.05, 0.05)]
    [InlineData(0.10, 0.10)]
    public void Constructor_WithRiskFreeRate_SetsCorrectValue(double? inputRate, double expectedRate)
    {
        var calculator = inputRate.HasValue
            ? new PerformanceMetricsCalculator(inputRate.Value)
            : new PerformanceMetricsCalculator();

        Assert.Equal(expectedRate, calculator.RiskFreeRate);
    }

    [Fact]
    public void RiskFreeRate_CanBeModified()
    {
        var calculator = new PerformanceMetricsCalculator(0.02);

        calculator.RiskFreeRate = 0.03;

        Assert.Equal(0.03, calculator.RiskFreeRate);
    }

    [Fact]
    public void CalculateMetrics_FiltersTradesByDate_ExcludesTradesOutsideRange()
    {
        var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var strategy = CreateBasicStrategy();

        var metrics = _calculator.CalculateMetrics(strategy, startDate, endDate);

        Assert.Equal(0, metrics.TotalTrades);
    }

    [Fact]
    public void WinRate_CalculatedUsingCompletedRoundTripTradesOnly()
    {
        // Arrange: Create strategy with trades that have different PnL values
        // Simulating Issue #2 from ZigZagBreakout analysis:
        // - 19 winning trades (PnL > 0)
        // - 13 losing trades (PnL < 0)
        // - 33 entry trades (PnL = 0)
        // Total = 65 trades
        // Incorrect win rate: 19/65 = 29.2%
        // Correct win rate: 19/(19+13) = 59.4%

        var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2020, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var strategy = CreateBasicStrategy();
        var baseTime = new DateTimeOffset(2020, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var myTrades = GetMyTradesCollection(strategy);

        // Add 19 winning trades (PnL > 0)
        for (var i = 0; i < 19; i++)
        {
            myTrades.Add(CreateMyTrade(strategy.Security, baseTime.AddHours(i), 100m + i, Sides.Buy));
        }

        // Add 13 losing trades (PnL < 0)
        for (var i = 0; i < 13; i++)
        {
            myTrades.Add(CreateMyTrade(strategy.Security, baseTime.AddHours(19 + i), -(50m + i), Sides.Sell));
        }

        // Add 33 entry trades (PnL = 0 or null - entries that don't represent completed round-trips)
        for (var i = 0; i < 33; i++)
        {
            myTrades.Add(CreateMyTrade(strategy.Security, baseTime.AddHours(32 + i), 0m, Sides.Buy));
        }

        // Act
        var metrics = _calculator.CalculateMetrics(strategy, startDate, endDate);

        // Assert: Win rate should be based on completed trades only (19 wins / 32 total completed)
        var expectedWinRate = 19.0 / (19 + 13) * 100; // 59.375%
        Assert.Equal(expectedWinRate, metrics.WinRate, precision: 2);
        Assert.Equal(19, metrics.WinningTrades);
        Assert.Equal(13, metrics.LosingTrades);
    }

    private static CachedSynchronizedSet<MyTrade> GetMyTradesCollection(Strategy strategy)
    {
        var field = typeof(Strategy).GetField("_myTrades", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find _myTrades field on Strategy");
        return (CachedSynchronizedSet<MyTrade>)field.GetValue(strategy)!;
    }

    private static MyTrade CreateMyTrade(Security security, DateTimeOffset time, decimal pnl, Sides side)
    {
        var order = new Order
        {
            Security = security,
            Side = side,
            TransactionId = time.Ticks,
            Price = 100m,
            Volume = 1m
        };

        var trade = new ExecutionMessage
        {
            SecurityId = security.ToSecurityId(),
            ServerTime = time.UtcDateTime,
            TradePrice = 100m,
            TradeVolume = 1m,
            TradeId = time.Ticks
        };

        return new MyTrade
        {
            Order = order,
            Trade = trade,
            PnL = pnl
        };
    }

    private static Strategy CreateBasicStrategy()
    {
        var security = new Security
        {
            Id = "TEST@TEST",
            Code = "TEST",
            PriceStep = 0.01m
        };

        var portfolio = Portfolio.CreateSimulator();
        portfolio.BeginValue = 10000m;
        portfolio.Name = "TestPortfolio";

        var strategy = new Strategy
        {
            Security = security,
            Portfolio = portfolio
        };

        return strategy;
    }
}
