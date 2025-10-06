using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.Tests;

public class WalkForwardModelsTests
{
    [Fact]
    public void AnchoredWindowGeneration_GeneratesCorrectWindows()
    {
        var config = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 3, 21, 0, 0, 0, TimeSpan.Zero); // 80 days total

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.Equal(5, windows.Count);

        // First window: Train from start (30 days), test next 10 days
        Assert.Equal(startDate, windows[0].trainStart);
        Assert.Equal(startDate.AddDays(30), windows[0].trainEnd);
        Assert.Equal(startDate.AddDays(30), windows[0].testStart);
        Assert.Equal(startDate.AddDays(40), windows[0].testEnd);

        // Second window: Train from start to 40 days (anchored), test next 10 days
        Assert.Equal(startDate, windows[1].trainStart);
        Assert.Equal(startDate.AddDays(40), windows[1].trainEnd);
        Assert.Equal(startDate.AddDays(40), windows[1].testStart);
        Assert.Equal(startDate.AddDays(50), windows[1].testEnd);

        // Third window: Train from start to 50 days (anchored), test next 10 days
        Assert.Equal(startDate, windows[2].trainStart);
        Assert.Equal(startDate.AddDays(50), windows[2].trainEnd);
    }

    [Fact]
    public void RollingWindowGeneration_GeneratesCorrectWindows()
    {
        var config = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Rolling
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 3, 21, 0, 0, 0, TimeSpan.Zero); // 80 days total

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.Equal(5, windows.Count);

        // First window: Train 30 days from start, test next 10 days
        Assert.Equal(startDate, windows[0].trainStart);
        Assert.Equal(startDate.AddDays(30), windows[0].trainEnd);
        Assert.Equal(startDate.AddDays(30), windows[0].testStart);
        Assert.Equal(startDate.AddDays(40), windows[0].testEnd);

        // Second window: Train days 10-40 (rolling), test next 10 days
        Assert.Equal(startDate.AddDays(10), windows[1].trainStart);
        Assert.Equal(startDate.AddDays(40), windows[1].trainEnd);
        Assert.Equal(startDate.AddDays(40), windows[1].testStart);
        Assert.Equal(startDate.AddDays(50), windows[1].testEnd);

        // Third window: Train days 20-50 (rolling), test next 10 days
        Assert.Equal(startDate.AddDays(20), windows[2].trainStart);
        Assert.Equal(startDate.AddDays(50), windows[2].trainEnd);
    }

    [Fact]
    public void WalkForwardConfig_InsufficientData_ReturnsEmpty()
    {
        var config = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero); // Only 20 days

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.Empty(windows);
    }

    [Fact]
    public void WalkForwardConfig_InvalidDates_ReturnsEmpty()
    {
        var config = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero); // End before start

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.Empty(windows);
    }

    [Fact]
    public void WindowResult_CalculatesPerformanceDegradation()
    {
        var trainMetrics = new PerformanceMetrics
        {
            TotalReturn = 20.0,
            SharpeRatio = 2.0,
            MaxDrawdown = -10.0
        };

        var testMetrics = new PerformanceMetrics
        {
            TotalReturn = 15.0,
            SharpeRatio = 1.5,
            MaxDrawdown = -12.0
        };

        var windowResult = new WindowResult
        {
            WindowNumber = 1,
            TrainingMetrics = trainMetrics,
            TestingMetrics = testMetrics,
            TrainingPeriod = (new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                              new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero)),
            TestingPeriod = (new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
                             new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero))
        };

        // Degradation = (Test - Train) / Train = (15 - 20) / 20 = -0.25 = -25%
        var expectedDegradation = (15.0 - 20.0) / 20.0;
        Assert.Equal(expectedDegradation, windowResult.PerformanceDegradation, 0.0001);
    }

    [Fact]
    public void WindowResult_ZeroTrainingReturn_HandlesCorrectly()
    {
        var trainMetrics = new PerformanceMetrics
        {
            TotalReturn = 0.0,
            SharpeRatio = 0.0
        };

        var testMetrics = new PerformanceMetrics
        {
            TotalReturn = 10.0,
            SharpeRatio = 1.0
        };

        var windowResult = new WindowResult
        {
            WindowNumber = 1,
            TrainingMetrics = trainMetrics,
            TestingMetrics = testMetrics,
            TrainingPeriod = (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(30)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(30), DateTimeOffset.Now.AddDays(40))
        };

        // Should return 0 to avoid division by zero
        Assert.Equal(0.0, windowResult.PerformanceDegradation);
    }

    [Fact]
    public void WalkForwardResult_CalculatesWalkForwardEfficiency()
    {
        var window1 = new WindowResult
        {
            WindowNumber = 1,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0 },
            TestingMetrics = new PerformanceMetrics { TotalReturn = 15.0 },
            TrainingPeriod = (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(30)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(30), DateTimeOffset.Now.AddDays(40))
        };

        var window2 = new WindowResult
        {
            WindowNumber = 2,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = 30.0 },
            TestingMetrics = new PerformanceMetrics { TotalReturn = 25.0 },
            TrainingPeriod = (DateTimeOffset.Now.AddDays(10), DateTimeOffset.Now.AddDays(40)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(40), DateTimeOffset.Now.AddDays(50))
        };

        var wfResult = new WalkForwardResult
        {
            TotalWindows = 2,
            Windows = [window1, window2]
        };

        // WF Efficiency = avgOOS / avgIS = (15 + 25) / 2 / ((20 + 30) / 2) = 20 / 25 = 0.8
        var expectedEfficiency = 20.0 / 25.0;
        Assert.Equal(expectedEfficiency, wfResult.WalkForwardEfficiency, 0.0001);
    }

    [Fact]
    public void WalkForwardResult_CalculatesConsistency()
    {
        var window1 = new WindowResult
        {
            WindowNumber = 1,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0 },
            TestingMetrics = new PerformanceMetrics { TotalReturn = 10.0 },
            TrainingPeriod = (DateTimeOffset.Now, DateTimeOffset.Now.AddDays(30)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(30), DateTimeOffset.Now.AddDays(40))
        };

        var window2 = new WindowResult
        {
            WindowNumber = 2,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = 30.0 },
            TestingMetrics = new PerformanceMetrics { TotalReturn = 20.0 },
            TrainingPeriod = (DateTimeOffset.Now.AddDays(10), DateTimeOffset.Now.AddDays(40)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(40), DateTimeOffset.Now.AddDays(50))
        };

        var window3 = new WindowResult
        {
            WindowNumber = 3,
            TrainingMetrics = new PerformanceMetrics { TotalReturn = 40.0 },
            TestingMetrics = new PerformanceMetrics { TotalReturn = 30.0 },
            TrainingPeriod = (DateTimeOffset.Now.AddDays(20), DateTimeOffset.Now.AddDays(50)),
            TestingPeriod = (DateTimeOffset.Now.AddDays(50), DateTimeOffset.Now.AddDays(60))
        };

        var wfResult = new WalkForwardResult
        {
            TotalWindows = 3,
            Windows = [window1, window2, window3]
        };

        // Test performances: 10, 20, 30
        // Mean = 20, Variance = ((10-20)^2 + (20-20)^2 + (30-20)^2) / 3 = (100 + 0 + 100) / 3 = 66.67
        // StdDev = sqrt(66.67) ≈ 8.165
        var expectedStdDev = Math.Sqrt(200.0 / 3.0);
        Assert.Equal(expectedStdDev, wfResult.Consistency, 0.01);
    }

    [Fact]
    public void WalkForwardResult_NoWindows_HandlesGracefully()
    {
        var wfResult = new WalkForwardResult
        {
            TotalWindows = 0,
            Windows = []
        };

        Assert.Equal(0.0, wfResult.WalkForwardEfficiency);
        Assert.Equal(0.0, wfResult.Consistency);
    }

    [Fact]
    public void WalkForwardConfig_DefaultMode_IsAnchored()
    {
        var config = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10)
        };

        Assert.Equal(WindowGenerationMode.Anchored, config.Mode);
    }
}
