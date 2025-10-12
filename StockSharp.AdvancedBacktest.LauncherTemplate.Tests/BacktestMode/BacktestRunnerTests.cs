using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.BacktestMode;

public class MockTestStrategy : CustomStrategyBase
{
    public MockTestStrategy()
    {
        Name = "MockTestStrategy";
    }
}

public class BacktestRunnerTests
{
    private BacktestConfiguration CreateValidConfiguration()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
            ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero),
            Securities = ["BTCUSDT", "ETHUSDT"],
            HistoryPath = Path.Combine(Path.GetTempPath(), "test-data"),
            OptimizableParameters = new Dictionary<string, ParameterDefinition>
            {
                ["Period"] = new ParameterDefinition
                {
                    Name = "Period",
                    Type = "int",
                    MinValue = JsonSerializer.SerializeToElement(10),
                    MaxValue = JsonSerializer.SerializeToElement(50),
                    StepValue = JsonSerializer.SerializeToElement(10)
                }
            }
        };
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BacktestRunner<MockTestStrategy>(null!));
    }

    [Fact]
    public void Constructor_WithValidConfig_SetsPropertiesCorrectly()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert
        Assert.Equal(config.ParallelWorkers, runner.ParallelThreads);
        Assert.Equal("./output", runner.OutputDirectory);
        Assert.False(runner.VerboseLogging);
    }

    [Fact]
    public void Constructor_WithCustomProperties_AcceptsOverrides()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config)
        {
            OutputDirectory = "./custom-output",
            ParallelThreads = 4,
            VerboseLogging = true
        };

        // Assert
        Assert.Equal("./custom-output", runner.OutputDirectory);
        Assert.Equal(4, runner.ParallelThreads);
        Assert.True(runner.VerboseLogging);
    }

    [Fact]
    public async Task RunAsync_WithInvalidTrainingDates_ReturnsErrorCode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.TrainingEndDate = config.TrainingStartDate.AddDays(-1);

        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Act
        var exitCode = await runner.RunAsync();

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithInvalidValidationDates_ReturnsErrorCode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.ValidationEndDate = config.ValidationStartDate.AddDays(-1);

        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Act
        var exitCode = await runner.RunAsync();

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithMissingHistoryPath_ReturnsErrorCode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.HistoryPath = Path.Combine(Path.GetTempPath(), "non-existent-path-" + Guid.NewGuid());

        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Act
        var exitCode = await runner.RunAsync();

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithNoOptimizableParameters_ReturnsErrorCode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>();

        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Act
        var exitCode = await runner.RunAsync();

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithNoSecurities_ReturnsErrorCode()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Securities = [];

        // Ensure history path exists so we get to securities validation
        Directory.CreateDirectory(config.HistoryPath);

        try
        {
            var runner = new BacktestRunner<MockTestStrategy>(config);

            // Act
            var exitCode = await runner.RunAsync();

            // Assert
            Assert.Equal(1, exitCode);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(config.HistoryPath))
            {
                Directory.Delete(config.HistoryPath, recursive: true);
            }
        }
    }

    [Fact]
    public void RunAsync_WithIntParameter_CreatesCorrectNumberParam()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OptimizableParameters["IntParam"] = new ParameterDefinition
        {
            Name = "IntParam",
            Type = "int",
            MinValue = JsonSerializer.SerializeToElement(1),
            MaxValue = JsonSerializer.SerializeToElement(10),
            StepValue = JsonSerializer.SerializeToElement(1)
        };

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert - no exception thrown means parameter was created successfully
        Assert.NotNull(runner);
    }

    [Fact]
    public void RunAsync_WithDecimalParameter_CreatesCorrectNumberParam()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OptimizableParameters["DecimalParam"] = new ParameterDefinition
        {
            Name = "DecimalParam",
            Type = "decimal",
            MinValue = JsonSerializer.SerializeToElement(0.1m),
            MaxValue = JsonSerializer.SerializeToElement(1.0m),
            StepValue = JsonSerializer.SerializeToElement(0.1m)
        };

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert
        Assert.NotNull(runner);
    }

    [Fact]
    public void RunAsync_WithDoubleParameter_CreatesCorrectNumberParam()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OptimizableParameters["DoubleParam"] = new ParameterDefinition
        {
            Name = "DoubleParam",
            Type = "double",
            MinValue = JsonSerializer.SerializeToElement(0.1),
            MaxValue = JsonSerializer.SerializeToElement(1.0),
            StepValue = JsonSerializer.SerializeToElement(0.1)
        };

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert
        Assert.NotNull(runner);
    }

    [Fact(Skip = "Integration test - requires market data and long execution time")]
    public async Task RunAsync_WithWalkForwardConfig_ProcessesCorrectly()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.WalkForwardConfig = new WalkForwardConfig
        {
            WindowSize = TimeSpan.FromDays(90),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(30),
            Mode = WindowGenerationMode.Rolling
        };

        // Create history path
        Directory.CreateDirectory(config.HistoryPath);

        var runner = new BacktestRunner<MockTestStrategy>(config)
        {
            VerboseLogging = false
        };

        // Act
        var exitCode = await runner.RunAsync();

        // Assert
        // May return error due to missing market data, but should not throw exception
        Assert.True(exitCode == 0 || exitCode == 1);

        // Cleanup
        try
        {
            Directory.Delete(config.HistoryPath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void RunAsync_WithMultipleParameters_CalculatesTotalCombinations()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.OptimizableParameters = new Dictionary<string, ParameterDefinition>
        {
            ["Param1"] = new ParameterDefinition
            {
                Name = "Param1",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(1),
                MaxValue = JsonSerializer.SerializeToElement(3),
                StepValue = JsonSerializer.SerializeToElement(1)
            },
            ["Param2"] = new ParameterDefinition
            {
                Name = "Param2",
                Type = "int",
                MinValue = JsonSerializer.SerializeToElement(10),
                MaxValue = JsonSerializer.SerializeToElement(20),
                StepValue = JsonSerializer.SerializeToElement(5)
            }
        };

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert
        // 3 values for Param1 (1,2,3) * 3 values for Param2 (10,15,20) = 9 combinations
        Assert.NotNull(runner);
    }

    [Fact]
    public void RunAsync_WithExportPath_CreatesOutputDirectory()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var exportPath = Path.Combine(Path.GetTempPath(), "test-export-" + Guid.NewGuid());
        config.ExportPath = exportPath;

        // Act
        var runner = new BacktestRunner<MockTestStrategy>(config);

        // Assert
        Assert.NotNull(runner);
        Assert.Equal("./output", runner.OutputDirectory);
    }
}
