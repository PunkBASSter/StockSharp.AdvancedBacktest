using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Backtest;

namespace StockSharp.AdvancedBacktest.Tests;

public class OptimizationLauncherWalkForwardTests
{
    private class MockStrategy : CustomStrategyBase
    {
        public MockStrategy() : base()
        {
        }
    }

    [Fact]
    public void WithWalkForward_ValidConfig_StoresConfiguration()
    {
        // Arrange
        var periodConfig = new PeriodConfig
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 1, 30, 0, 0, 0, TimeSpan.Zero)
        };

        var mockOptimizerRunner = new OptimizerRunner<MockStrategy>();
        var launcher = new OptimizationLauncher<MockStrategy>(periodConfig, mockOptimizerRunner);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Anchored
        };

        // Act
        var result = launcher.WithWalkForward(wfConfig);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OptimizationLauncher<MockStrategy>>(result);
    }

    [Fact]
    public void WithWalkForward_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var periodConfig = new PeriodConfig
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 1, 30, 0, 0, 0, TimeSpan.Zero)
        };

        var mockOptimizerRunner = new OptimizerRunner<MockStrategy>();
        var launcher = new OptimizationLauncher<MockStrategy>(periodConfig, mockOptimizerRunner);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => launcher.WithWalkForward(null!));
    }

    [Fact]
    public void WithWalkForward_SupportsFluentChaining()
    {
        // Arrange
        var periodConfig = new PeriodConfig
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 1, 30, 0, 0, 0, TimeSpan.Zero)
        };

        var mockOptimizerRunner = new OptimizerRunner<MockStrategy>();
        var launcher = new OptimizationLauncher<MockStrategy>(periodConfig, mockOptimizerRunner);

        var wfConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(30),
            StepSize = TimeSpan.FromDays(10),
            ValidationSize = TimeSpan.FromDays(10),
            Mode = WindowGenerationMode.Rolling
        };

        // Act
        var result = launcher
            .WithWalkForward(wfConfig)
            .WithOptimizationThreads(4);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OptimizationLauncher<MockStrategy>>(result);
    }

    [Fact]
    public void OptimizationResult_CanStoreWalkForwardResult()
    {
        // Arrange
        var config = new OptimizationConfig
        {
            ParamsContainer = new CustomParamsContainer(Enumerable.Empty<ICustomParam>()),
            TrainingPeriod = new PeriodConfig
            {
                StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero)
            },
            ValidationPeriod = new PeriodConfig
            {
                StartDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero)
            },
            HistoryPath = "C:\\Data\\History",
            InitialCapital = 10000m
        };

        var wfResult = new WalkForwardResult
        {
            TotalWindows = 3,
            Windows = new List<WindowResult>
            {
                new WindowResult
                {
                    WindowNumber = 1,
                    TrainingMetrics = new PerformanceMetrics { TotalReturn = 20.0 },
                    TestingMetrics = new PerformanceMetrics { TotalReturn = 15.0 },
                    TrainingPeriod = (new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero)),
                    TestingPeriod = (new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero))
                }
            }
        };

        // Act
        var optimizationResult = new OptimizationResult<MockStrategy>
        {
            Config = config,
            TrainedStrategy = new MockStrategy(),
            WalkForwardResult = wfResult
        };

        // Assert
        Assert.NotNull(optimizationResult.WalkForwardResult);
        Assert.Equal(3, optimizationResult.WalkForwardResult.TotalWindows);
        Assert.Single(optimizationResult.WalkForwardResult.Windows);
    }

    [Fact]
    public void OptimizationResult_WalkForwardResult_CanBeNull()
    {
        // Arrange
        var config = new OptimizationConfig
        {
            ParamsContainer = new CustomParamsContainer(Enumerable.Empty<ICustomParam>()),
            TrainingPeriod = new PeriodConfig
            {
                StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero)
            },
            ValidationPeriod = new PeriodConfig
            {
                StartDate = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero),
                EndDate = new DateTimeOffset(2024, 2, 10, 0, 0, 0, TimeSpan.Zero)
            },
            HistoryPath = "C:\\Data\\History",
            InitialCapital = 10000m
        };

        // Act
        var optimizationResult = new OptimizationResult<MockStrategy>
        {
            Config = config,
            TrainedStrategy = new MockStrategy(),
            WalkForwardResult = null
        };

        // Assert
        Assert.Null(optimizationResult.WalkForwardResult);
    }
}
