using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Pipeline;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Tests.Pipeline;

public class PipelineResultTests
{
    private sealed class TestStrategy : CustomStrategyBase
    {
    }

    private static PipelineResult<TestStrategy> CreateMinimalResult()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = ["BTCUSDT"],
            TimeFrames = [TimeSpan.FromMinutes(5)],
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config
        };

        var startTime = DateTimeOffset.UtcNow;
        var completionTime = startTime.AddMinutes(10);

        return new PipelineResult<TestStrategy>
        {
            StartTime = startTime,
            CompletionTime = completionTime,
            IsSuccess = true,
            FinalContext = context
        };
    }

    [Fact]
    public void CreateResult_WithRequiredFields_Succeeds()
    {
        var result = CreateMinimalResult();

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.FinalContext);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Result_Duration_CalculatesCorrectly()
    {
        var start = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var completion = new DateTimeOffset(2024, 1, 1, 10, 30, 0, TimeSpan.Zero);

        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config
        };

        var result = new PipelineResult<TestStrategy>
        {
            StartTime = start,
            CompletionTime = completion,
            IsSuccess = true,
            FinalContext = context
        };

        Assert.Equal(TimeSpan.FromMinutes(30), result.Duration);
    }

    [Fact]
    public void Result_WithErrorMessage_IsFailure()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config
        };

        var result = new PipelineResult<TestStrategy>
        {
            StartTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddSeconds(5),
            IsSuccess = false,
            ErrorMessage = "Pipeline failed",
            FinalContext = context
        };

        Assert.False(result.IsSuccess);
        Assert.Equal("Pipeline failed", result.ErrorMessage);
    }

    [Fact]
    public void Result_WithException_IsFailure()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config
        };

        var ex = new InvalidOperationException("Test exception");
        var result = new PipelineResult<TestStrategy>
        {
            StartTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddSeconds(5),
            IsSuccess = false,
            Exception = ex,
            FinalContext = context
        };

        Assert.False(result.IsSuccess);
        Assert.Equal(ex, result.Exception);
    }

    [Fact]
    public void BestStrategy_ReturnsFirstValidatedResult()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var validatedResults = new List<OptimizationResult<TestStrategy>>
        {
            new OptimizationResult<TestStrategy>
            {
                Config = new Models.OptimizationConfig
                {
                    HistoryPath = "C:\\Data",
                    ParamsContainer = new Parameters.CustomParamsContainer(new Parameters.ICustomParam[0]),
                    TrainingPeriod = new Models.OptimizationPeriodConfig
                    {
                        TrainingStartDate = config.TrainingStartDate,
                        TrainingEndDate = config.TrainingEndDate
                    }
                },
                TrainedStrategy = new TestStrategy()
            }
        };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config,
            ValidatedResults = validatedResults
        };

        var result = new PipelineResult<TestStrategy>
        {
            StartTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddMinutes(10),
            IsSuccess = true,
            FinalContext = context
        };

        Assert.NotNull(result.BestStrategy);
        Assert.Equal(validatedResults[0], result.BestStrategy);
    }

    [Fact]
    public void BestStrategy_WhenNoValidatedResults_ReturnsNull()
    {
        var result = CreateMinimalResult();
        Assert.Null(result.BestStrategy);
    }

    [Fact]
    public void ValidatedResults_ReturnsContextValidatedResults()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var validatedResults = new List<OptimizationResult<TestStrategy>>();

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config,
            ValidatedResults = validatedResults
        };

        var result = new PipelineResult<TestStrategy>
        {
            StartTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddMinutes(10),
            IsSuccess = true,
            FinalContext = context
        };

        Assert.Equal(validatedResults, result.ValidatedResults);
    }

    [Fact]
    public void ExportedArtifacts_ReturnsContextExportedArtifacts()
    {
        var config = new PipelineConfiguration
        {
            HistoryPath = "C:\\Data",
            Securities = new[] { "BTCUSDT" },
            TimeFrames = new[] { TimeSpan.FromMinutes(5) },
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero)
        };

        var exportedArtifacts = new List<string> { "artifact1.json", "artifact2.csv" };

        var context = new PipelineContext<TestStrategy>
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            PipelineId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
            LaunchMode = LaunchMode.Optimization,
            Configuration = config,
            ExportedArtifacts = exportedArtifacts
        };

        var result = new PipelineResult<TestStrategy>
        {
            StartTime = DateTimeOffset.UtcNow,
            CompletionTime = DateTimeOffset.UtcNow.AddMinutes(10),
            IsSuccess = true,
            FinalContext = context
        };

        Assert.Equal(exportedArtifacts, result.ExportedArtifacts);
    }
}
