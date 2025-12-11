using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Tests.Statistics;

public class PerformanceMetricsCalculatorTests
{
    [Fact]
    public void Constructor_SetsDefaultRiskFreeRate()
    {
        var calculator = new PerformanceMetricsCalculator();

        Assert.Equal(0.02, calculator.RiskFreeRate);
    }

    [Fact]
    public void Constructor_AcceptsCustomRiskFreeRate()
    {
        var calculator = new PerformanceMetricsCalculator(0.05);

        Assert.Equal(0.05, calculator.RiskFreeRate);
    }

    [Fact]
    public void ImplementsIPerformanceMetricsCalculator()
    {
        var calculator = new PerformanceMetricsCalculator();

        Assert.IsAssignableFrom<IPerformanceMetricsCalculator>(calculator);
    }
}

public class IPerformanceMetricsCalculatorContractTests
{
    [Fact]
    public void Interface_DefinesCalculateMetricsMethod()
    {
        var method = typeof(IPerformanceMetricsCalculator).GetMethod("CalculateMetrics");

        Assert.NotNull(method);
        Assert.Equal(typeof(PerformanceMetrics), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
    }
}
