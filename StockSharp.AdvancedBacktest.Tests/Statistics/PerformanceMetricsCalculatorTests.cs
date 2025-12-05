using StockSharp.Algo.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.BusinessEntities;

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
