using StockSharp.AdvancedBacktest.Utilities;

namespace StockSharp.AdvancedBacktest.Tests;

public class MetricsCalculationTests
{
    [Fact]
    public void CalculateMaxDrawdown_WithGrowingEquity_ReturnsZero()
    {
        var equity = new List<decimal> { 10000, 11000, 12000, 13000 };

        var result = StatisticsCalculator.CalculateMaxDrawdown(equity);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateMaxDrawdown_WithSingleDrop_ReturnsCorrectPercentage()
    {
        var equity = new List<decimal> { 10000, 12000, 9000 };

        var result = StatisticsCalculator.CalculateMaxDrawdown(equity);

        Assert.Equal(25.0, result, 2);
    }

    [Fact]
    public void CalculateMaxDrawdown_WithMultipleDrops_ReturnsLargestDrawdown()
    {
        var equity = new List<decimal> { 10000, 15000, 12000, 14000, 10000 };

        var result = StatisticsCalculator.CalculateMaxDrawdown(equity);

        // Max drawdown is from peak of 15000 to low of 10000: (15000-10000)/15000 = 33.33%
        Assert.Equal(33.33, result, 2);
    }

    [Fact]
    public void CalculateMaxDrawdown_EmptyList_ReturnsZero()
    {
        var equity = new List<decimal>();

        var result = StatisticsCalculator.CalculateMaxDrawdown(equity);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateSharpeRatio_WithPositiveReturns_ReturnsPositiveValue()
    {
        var returns = new List<double> { 0.01, 0.02, 0.015, 0.018, 0.012 };

        var result = StatisticsCalculator.CalculateSharpeRatio(returns, 0.02);

        Assert.True(result > 0);
    }

    [Fact]
    public void CalculateSharpeRatio_WithZeroVolatility_ReturnsZero()
    {
        var returns = new List<double> { 0.01, 0.01, 0.01, 0.01 };

        var result = StatisticsCalculator.CalculateSharpeRatio(returns, 0.02);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateSortinoRatio_WithOnlyPositiveReturns_ReturnsInfinity()
    {
        var returns = new List<double> { 0.01, 0.02, 0.015, 0.018 };

        var result = StatisticsCalculator.CalculateSortinoRatio(returns, 0.02);

        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void CalculateSortinoRatio_WithMixedReturns_ReturnsPositiveValue()
    {
        var returns = new List<double> { 0.02, -0.01, 0.03, -0.005, 0.015 };

        var result = StatisticsCalculator.CalculateSortinoRatio(returns, 0.02);

        Assert.True(result > 0);
        Assert.NotEqual(double.PositiveInfinity, result);
    }

    [Fact]
    public void CalculateWinRate_WithWins_ReturnsCorrectPercentage()
    {
        var result = StatisticsCalculator.CalculateWinRate(60, 100);

        Assert.Equal(60.0, result);
    }

    [Fact]
    public void CalculateWinRate_WithZeroTrades_ReturnsZero()
    {
        var result = StatisticsCalculator.CalculateWinRate(0, 0);

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateProfitFactor_WithProfitableStrategy_ReturnsGreaterThanOne()
    {
        var result = StatisticsCalculator.CalculateProfitFactor(15000, 10000);

        Assert.Equal(1.5, result);
        Assert.True(result > 1);
    }

    [Fact]
    public void CalculateProfitFactor_WithZeroLoss_ReturnsInfinity()
    {
        var result = StatisticsCalculator.CalculateProfitFactor(15000, 0);

        Assert.Equal(double.PositiveInfinity, result);
    }

    [Fact]
    public void CalculateProfitFactor_WithLosingStrategy_ReturnsLessThanOne()
    {
        var result = StatisticsCalculator.CalculateProfitFactor(5000, 10000);

        Assert.Equal(0.5, result);
        Assert.True(result < 1);
    }
}
