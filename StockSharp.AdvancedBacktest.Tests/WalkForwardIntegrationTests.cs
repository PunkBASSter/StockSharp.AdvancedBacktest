using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Backtest;

namespace StockSharp.AdvancedBacktest.Tests;

public class WalkForwardIntegrationTests
{
    private class MockStrategy : CustomStrategyBase
    {
        public MockStrategy() : base()
        {
        }
    }

    private OptimizationConfig CreateMockConfig()
    {
        var paramsContainer = new CustomParamsContainer(Enumerable.Empty<ICustomParam>());

        var trainingPeriod = new PeriodConfig
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero)
        };

        var validationPeriod = new PeriodConfig
        {
            StartDate = new DateTimeOffset(2024, 3, 31, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 4, 30, 0, 0, 0, TimeSpan.Zero)
        };

        return new OptimizationConfig
        {
            ParamsContainer = paramsContainer,
            TrainingPeriod = trainingPeriod,
            ValidationPeriod = validationPeriod,
            HistoryPath = "C:\\Data\\History",
            InitialCapital = 10000m,
            TradeVolume = 0.01m
        };
    }

    [Fact]
    public void ThreeFoldWalkForward_CompletesSuccessfully()
    {
        // Arrange
        var baseConfig = CreateMockConfig();

        // Create mock optimizer that returns different metrics for each window
        var windowCount = 0;
        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            windowCount++;

            // Simulate different performance for each window
            var trainReturn = 20.0 + (windowCount * 5.0);  // Increasing training performance
            var testReturn = 15.0 + (windowCount * 3.0);   // Testing performance lags a bit

            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                [$"result_{windowCount}"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics
                    {
                        TotalReturn = trainReturn,
                        SharpeRatio = 2.0 + (windowCount * 0.1),
                        SortinoRatio = 2.5 + (windowCount * 0.1),
                        MaxDrawdown = -5.0
                    },
                    ValidationMetrics = new PerformanceMetrics
                    {
                        TotalReturn = testReturn,
                        SharpeRatio = 1.5 + (windowCount * 0.1),
                        SortinoRatio = 2.0 + (windowCount * 0.1),
                        MaxDrawdown = -7.0
                    }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        // Configure for exactly 3 windows
        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),    // 30 day training window
            StepSize = TimeSpan.FromDays(30),       // Step forward 30 days each time
            ValidationSize = TimeSpan.FromDays(10), // 10 day testing window
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero); // 100 days total for 3 windows

        // Act
        var result = validator.Validate(wfConfig, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalWindows);
        Assert.Equal(3, result.Windows.Count);

        // Verify each window has metrics
        foreach (var window in result.Windows)
        {
            Assert.NotNull(window.TrainingMetrics);
            Assert.NotNull(window.TestingMetrics);
            Assert.True(window.TrainingMetrics.TotalReturn > 0);
            Assert.True(window.TestingMetrics.TotalReturn > 0);
        }

        // Verify WF efficiency is calculated
        Assert.True(result.WalkForwardEfficiency > 0);
        Assert.True(result.WalkForwardEfficiency <= 1.0);

        // Verify consistency is calculated
        Assert.True(result.Consistency >= 0);
    }

    [Fact]
    public void WalkForwardEfficiency_MatchesManualCalculation()
    {
        // Arrange
        var baseConfig = CreateMockConfig();

        // Use fixed metrics for predictable calculation
        var testMetrics = new[]
        {
            (trainReturn: 20.0, testReturn: 15.0),  // Window 1: Degradation of 25%
			(trainReturn: 30.0, testReturn: 25.0),  // Window 2: Degradation of 16.67%
			(trainReturn: 25.0, testReturn: 20.0)   // Window 3: Degradation of 20%
		};

        var windowIndex = 0;
        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            var metrics = testMetrics[windowIndex];
            windowIndex++;

            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics
                    {
                        TotalReturn = metrics.trainReturn,
                        SharpeRatio = 2.0
                    },
                    ValidationMetrics = new PerformanceMetrics
                    {
                        TotalReturn = metrics.testReturn,
                        SharpeRatio = 1.5
                    }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Rolling
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero); // 100 days for 3 windows

        // Act
        var result = validator.Validate(wfConfig, startDate, endDate);

        // Assert - Manual calculation of WF Efficiency
        // avgIS = (20 + 30 + 25) / 3 = 25.0
        // avgOOS = (15 + 25 + 20) / 3 = 20.0
        // WF Efficiency = avgOOS / avgIS = 20.0 / 25.0 = 0.8
        var expectedEfficiency = 20.0 / 25.0;

        Assert.NotNull(result);
        Assert.Equal(3, result.Windows.Count);
        Assert.Equal(expectedEfficiency, result.WalkForwardEfficiency, 0.0001);
    }

    [Fact]
    public void WindowGeneration_ProducesCorrectWindowCount()
    {
        // Arrange
        var baseConfig = CreateMockConfig();

        var callCount = 0;
        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            callCount++;
            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0, SharpeRatio = 2.0 },
                    ValidationMetrics = new PerformanceMetrics { TotalReturn = 15.0, SharpeRatio = 1.5 }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        // Test Anchored mode - should produce 3 windows
        var anchoredConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero); // 100 days for 3 windows

        // Act - Anchored
        callCount = 0;
        var anchoredResult = validator.Validate(anchoredConfig, startDate, endDate);

        // Assert - Anchored
        Assert.Equal(3, anchoredResult.TotalWindows);
        Assert.Equal(3, callCount); // Optimizer called 3 times

        // Test Rolling mode - should also produce 3 windows
        var rollingConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Rolling
        };

        // Act - Rolling
        callCount = 0;
        var rollingResult = validator.Validate(rollingConfig, startDate, endDate);

        // Assert - Rolling
        Assert.Equal(3, rollingResult.TotalWindows);
        Assert.Equal(3, callCount); // Optimizer called 3 times

        // Test insufficient data - should produce 0 windows
        var insufficientEndDate = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero); // Only 20 days

        // Act - Insufficient data
        callCount = 0;
        var insufficientResult = validator.Validate(anchoredConfig, startDate, insufficientEndDate);

        // Assert - Insufficient data
        Assert.Equal(0, insufficientResult.TotalWindows);
        Assert.Empty(insufficientResult.Windows);
        Assert.Equal(0, callCount); // Optimizer not called
    }

    [Fact]
    public void MetricsComparison_InSampleVsOutOfSample_WorksCorrectly()
    {
        // Arrange
        var baseConfig = CreateMockConfig();

        // Create scenarios where training outperforms testing (realistic overfitting scenario)
        var scenarios = new[]
        {
            (trainReturn: 25.0, testReturn: 18.0, trainSharpe: 2.5, testSharpe: 1.8),
            (trainReturn: 30.0, testReturn: 22.0, trainSharpe: 2.8, testSharpe: 2.0),
            (trainReturn: 22.0, testReturn: 16.0, trainSharpe: 2.2, testSharpe: 1.5)
        };

        var windowIndex = 0;
        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            var scenario = scenarios[windowIndex];
            windowIndex++;

            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics
                    {
                        TotalReturn = scenario.trainReturn,
                        SharpeRatio = scenario.trainSharpe,
                        SortinoRatio = scenario.trainSharpe + 0.5
                    },
                    ValidationMetrics = new PerformanceMetrics
                    {
                        TotalReturn = scenario.testReturn,
                        SharpeRatio = scenario.testSharpe,
                        SortinoRatio = scenario.testSharpe + 0.5
                    }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero); // 100 days for 3 windows

        // Act
        var result = validator.Validate(wfConfig, startDate, endDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Windows.Count);

        // Verify metrics comparison for each window
        for (int i = 0; i < result.Windows.Count; i++)
        {
            var window = result.Windows[i];
            var scenario = scenarios[i];

            // Training metrics should match input
            Assert.Equal(scenario.trainReturn, window.TrainingMetrics.TotalReturn);
            Assert.Equal(scenario.trainSharpe, window.TrainingMetrics.SharpeRatio);

            // Testing metrics should match input
            Assert.Equal(scenario.testReturn, window.TestingMetrics.TotalReturn);
            Assert.Equal(scenario.testSharpe, window.TestingMetrics.SharpeRatio);

            // Training should outperform testing (realistic scenario)
            Assert.True(window.TrainingMetrics.TotalReturn > window.TestingMetrics.TotalReturn);
            Assert.True(window.TrainingMetrics.SharpeRatio > window.TestingMetrics.SharpeRatio);

            // Verify performance degradation calculation
            var expectedDegradation = (scenario.testReturn - scenario.trainReturn) / scenario.trainReturn;
            Assert.Equal(expectedDegradation, window.PerformanceDegradation, 0.0001);
        }

        // Verify WF efficiency shows degradation (should be < 1.0)
        Assert.True(result.WalkForwardEfficiency < 1.0, "WF Efficiency should be less than 1.0 when testing underperforms training");

        // Verify consistency calculation
        Assert.True(result.Consistency > 0, "Consistency should be positive when there's variance in returns");
    }
}
