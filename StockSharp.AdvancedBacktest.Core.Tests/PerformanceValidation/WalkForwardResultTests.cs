using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Tests.PerformanceValidation;

public class WalkForwardResultTests
{
    [Fact]
    public void EmptyWindows_ReturnsZeroForAllMetrics()
    {
        var result = new WalkForwardResult { TotalWindows = 0, Windows = [] };

        Assert.Equal(0.0, result.WalkForwardEfficiency);
        Assert.Equal(0.0, result.Consistency);
    }

    [Fact]
    public void WalkForwardEfficiency_CalculatesAvgOOSDividedByAvgIS()
    {
        // avgOOS = (8 + 16) / 2 = 12, avgIS = (10 + 20) / 2 = 15, efficiency = 12/15 = 0.8
        var result = CreateResult((10.0, 8.0), (20.0, 16.0));
        Assert.Equal(0.8, result.WalkForwardEfficiency, 2);
    }

    [Fact]
    public void Consistency_ReturnsStdDevOfTestingReturns()
    {
        // Both testing returns are 10.0, std dev = 0
        var result = CreateResult((10.0, 10.0), (20.0, 10.0));
        Assert.Equal(0.0, result.Consistency, 2);
    }

    private static WalkForwardResult CreateResult(params (double train, double test)[] windows)
    {
        var now = DateTimeOffset.Now;
        return new WalkForwardResult
        {
            TotalWindows = windows.Length,
            Windows = windows.Select((w, i) => new WindowResult
            {
                WindowNumber = i + 1,
                TrainingMetrics = new PerformanceMetrics { TotalReturn = w.train },
                TestingMetrics = new PerformanceMetrics { TotalReturn = w.test },
                TrainingPeriod = (now.AddDays(-30), now),
                TestingPeriod = (now, now.AddDays(7))
            }).ToList()
        };
    }
}

public class WindowResultTests
{
    [Theory]
    [InlineData(0.0, 10.0, 0.0)]    // zero training return -> 0
    [InlineData(20.0, 15.0, -0.25)] // (15-20)/20 = -0.25
    [InlineData(10.0, 12.0, 0.2)]   // (12-10)/10 = 0.2
    public void PerformanceDegradation_Calculates(double trainReturn, double testReturn, double expected)
    {
        var now = DateTimeOffset.Now;
        var result = new WindowResult
        {
            WindowNumber = 1,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = trainReturn },
            TestingMetrics = new PerformanceMetrics { TotalReturn = testReturn },
            TrainingPeriod = (now.AddDays(-30), now),
            TestingPeriod = (now, now.AddDays(7))
        };

        Assert.Equal(expected, result.PerformanceDegradation, 2);
    }
}
