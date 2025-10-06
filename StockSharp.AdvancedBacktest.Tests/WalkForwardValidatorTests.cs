using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Parameters;

namespace StockSharp.AdvancedBacktest.Tests;

public class WalkForwardValidatorTests
{
    private class MockStrategy : CustomStrategyBase
    {
        public MockStrategy() : base()
        {
        }
    }

    private OptimizationConfig CreateMockConfig()
    {
        var paramsContainer = new CustomParamsContainer();

        var periodConfig = new OptimizationPeriodConfig
        {
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero)
        };

        return new OptimizationConfig
        {
            ParamsContainer = paramsContainer,
            TrainingPeriod = periodConfig,
            HistoryPath = "C:\\Data\\History",
            InitialCapital = 10000m,
            TradeVolume = 0.01m
        };
    }

    [Fact]
    public void WalkForward_Anchored_ThreeWindows_ProducesResults()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result1"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0, SharpeRatio = 2.0 },
                    ValidationMetrics = new PerformanceMetrics { TotalReturn = 15.0, SharpeRatio = 1.5 }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.True(result.Windows.Count > 0);
        Assert.Equal(result.Windows.Count, result.TotalWindows);
    }

    [Fact]
    public void WalkForward_Rolling_ThreeWindows_ProducesResults()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result1"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 25.0, SharpeRatio = 2.5 },
                    ValidationMetrics = new PerformanceMetrics { TotalReturn = 20.0, SharpeRatio = 2.0 }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Rolling
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.True(result.Windows.Count > 0);
        Assert.Equal(WindowGenerationMode.Rolling, wfConfig.Mode);
    }

    [Fact]
    public void WalkForward_CalculatesWFEfficiency()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result1"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 30.0, SharpeRatio = 2.5 },
                    ValidationMetrics = new PerformanceMetrics { TotalReturn = 24.0, SharpeRatio = 2.0 }
                }
            };
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(20),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.True(result.WalkForwardEfficiency > 0);
        Assert.True(result.WalkForwardEfficiency <= 1.0);
    }

    [Fact]
    public void WalkForward_TracksPerformanceDegradation()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>
            {
                ["result1"] = new OptimizationResult<MockStrategy>
                {
                    Config = config,
                    TrainedStrategy = new MockStrategy(),
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0, SharpeRatio = 2.0 },
                    ValidationMetrics = new PerformanceMetrics { TotalReturn = 15.0, SharpeRatio = 1.5 }
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
        var endDate = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.All(result.Windows, window =>
        {
            Assert.NotEqual(0.0, window.PerformanceDegradation);
        });
    }

    [Fact]
    public void WalkForward_NoValidStrategies_HandlesGracefully()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>();
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 2, 20, 0, 0, 0, TimeSpan.Zero);

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.Empty(result.Windows);
        Assert.Equal(0, result.TotalWindows);
    }

    [Fact]
    public void WalkForward_InsufficientData_ReturnsEmpty()
    {
        var baseConfig = CreateMockConfig();

        Func<OptimizationConfig, Dictionary<string, OptimizationResult<MockStrategy>>> mockOptimizer = (config) =>
        {
            return new Dictionary<string, OptimizationResult<MockStrategy>>();
        };

        var validator = new WalkForwardValidator<MockStrategy>(null!, baseConfig, mockOptimizer);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2024, 1, 20, 0, 0, 0, TimeSpan.Zero); // Only 20 days, insufficient

        var result = validator.Validate(wfConfig, startDate, endDate);

        Assert.NotNull(result);
        Assert.Empty(result.Windows);
        Assert.Equal(0, result.TotalWindows);
    }
}
