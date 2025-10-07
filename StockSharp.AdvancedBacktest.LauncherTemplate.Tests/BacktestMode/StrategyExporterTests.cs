using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.BacktestMode;

public class StrategyExporterTests : IDisposable
{
    private readonly string _tempDirectory;

    public StrategyExporterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"exporter-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void BuildConfiguration_WithValidResult_ReturnsConfigWithAllFields()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var strategy = CreateMockStrategy();
        var result = CreateOptimizationResult(strategy);
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var config = exporter.BuildConfiguration(result, backtestConfig);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(backtestConfig.StrategyName, config.StrategyName);
        Assert.Equal(strategy.Version, config.StrategyVersion);
        Assert.NotNull(config.StrategyHash);
        Assert.Equal(32, config.StrategyHash.Length); // SHA256 truncated to 32 chars
        Assert.Equal(result.StartTime, config.OptimizationDate);
        Assert.Equal(backtestConfig.InitialCapital, config.InitialCapital);
        Assert.Equal(backtestConfig.TradeVolume, config.TradeVolume);
        Assert.NotNull(config.Parameters);
        Assert.NotNull(config.Securities);
        Assert.Equal(result.TrainingMetrics, config.TrainingMetrics);
        Assert.Equal(result.ValidationMetrics, config.ValidationMetrics);
    }

    [Fact]
    public void BuildConfiguration_WithNullResult_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var backtestConfig = CreateBacktestConfiguration();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            exporter.BuildConfiguration(null!, backtestConfig));
    }

    [Fact]
    public void BuildConfiguration_WithNullBacktestConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var result = CreateOptimizationResult(CreateMockStrategy());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            exporter.BuildConfiguration(result, null!));
    }

    [Fact]
    public void BuildConfiguration_ExtractsParametersCorrectly()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var strategy = CreateMockStrategy();
        var result = CreateOptimizationResult(strategy);
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var config = exporter.BuildConfiguration(result, backtestConfig);

        // Assert
        Assert.NotNull(config.Parameters);
        Assert.True(config.Parameters.ContainsKey("Period"));
        Assert.Equal(20, config.Parameters["Period"].Deserialize<int>());
    }

    [Fact]
    public void BuildConfiguration_ExtractsSecuritiesCorrectly()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var strategy = CreateMockStrategy();
        var result = CreateOptimizationResult(strategy);
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var config = exporter.BuildConfiguration(result, backtestConfig);

        // Assert
        Assert.NotNull(config.Securities);
        Assert.Single(config.Securities);
        Assert.Contains("BTCUSDT@CRYPTO", config.Securities);
    }

    [Fact]
    public void BuildConfiguration_GeneratesConsistentHash()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var strategy = CreateMockStrategy();
        var result = CreateOptimizationResult(strategy);
        var backtestConfig = CreateBacktestConfiguration();

        // Act - Generate hash twice
        var config1 = exporter.BuildConfiguration(result, backtestConfig);
        var config2 = exporter.BuildConfiguration(result, backtestConfig);

        // Assert - Same strategy should produce same hash
        Assert.Equal(config1.StrategyHash, config2.StrategyHash);
    }

    [Fact]
    public async Task ExportAsync_WithValidConfig_CreatesFile()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var config = CreateStrategyParametersConfig();
        var filePath = Path.Combine(_tempDirectory, "test-strategy.json");

        // Act
        await exporter.ExportAsync(config, filePath);

        // Assert
        Assert.True(File.Exists(filePath));
        var json = await File.ReadAllTextAsync(filePath);
        Assert.NotEmpty(json);
        
        var deserialized = JsonSerializer.Deserialize<StrategyParametersConfig>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(config.StrategyName, deserialized.StrategyName);
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var config = CreateStrategyParametersConfig();
        var subDir = Path.Combine(_tempDirectory, "nested", "directory");
        var filePath = Path.Combine(subDir, "test-strategy.json");

        // Act
        await exporter.ExportAsync(config, filePath);

        // Assert
        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task ExportAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var filePath = Path.Combine(_tempDirectory, "test.json");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            exporter.ExportAsync(null!, filePath));
    }

    [Fact]
    public async Task ExportAsync_WithNullFilePath_ThrowsArgumentException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var config = CreateStrategyParametersConfig();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            exporter.ExportAsync(config, null!));
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_WithValidResults_ExportsTopN()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var results = CreateMultipleResults(10);
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var paths = await exporter.ExportTopStrategiesAsync(
            results, 
            backtestConfig, 
            _tempDirectory, 
            topCount: 5);

        // Assert
        Assert.Equal(5, paths.Count);
        Assert.All(paths, path => Assert.True(File.Exists(path)));
        Assert.Contains(paths, p => p.EndsWith("strategy_1.json"));
        Assert.Contains(paths, p => p.EndsWith("strategy_5.json"));
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_OrdersBySortinoRatio()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var results = new[]
        {
            CreateOptimizationResultWithMetrics(sortinoRatio: 1.5m),
            CreateOptimizationResultWithMetrics(sortinoRatio: 2.5m), // Best
            CreateOptimizationResultWithMetrics(sortinoRatio: 0.8m),
        };
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var paths = await exporter.ExportTopStrategiesAsync(
            results, 
            backtestConfig, 
            _tempDirectory, 
            topCount: 3);

        // Assert
        var firstStrategyJson = await File.ReadAllTextAsync(paths[0]);
        var firstStrategy = JsonSerializer.Deserialize<StrategyParametersConfig>(firstStrategyJson);
        
        // First exported should be the one with highest Sortino (2.5)
        Assert.Equal(2.5, firstStrategy!.ValidationMetrics!.SortinoRatio, precision: 2);
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_WithFewerResultsThanTopCount_ExportsAll()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var results = CreateMultipleResults(3);
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var paths = await exporter.ExportTopStrategiesAsync(
            results, 
            backtestConfig, 
            _tempDirectory, 
            topCount: 10);

        // Assert
        Assert.Equal(3, paths.Count);
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_FiltersOutNullValidationMetrics()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var results = new[]
        {
            CreateOptimizationResultWithMetrics(sortinoRatio: 1.5m),
            CreateOptimizationResultWithMetrics(sortinoRatio: null), // Should be filtered
            CreateOptimizationResultWithMetrics(sortinoRatio: 2.0m),
        };
        var backtestConfig = CreateBacktestConfiguration();

        // Act
        var paths = await exporter.ExportTopStrategiesAsync(
            results, 
            backtestConfig, 
            _tempDirectory, 
            topCount: 5);

        // Assert
        Assert.Equal(2, paths.Count); // Only 2 with valid metrics
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_WithNullResults_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var backtestConfig = CreateBacktestConfiguration();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            exporter.ExportTopStrategiesAsync(
                null!, 
                backtestConfig, 
                _tempDirectory));
    }

    [Fact]
    public async Task ExportTopStrategiesAsync_WithInvalidTopCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var exporter = new StrategyExporter<MockTestStrategy>();
        var results = CreateMultipleResults(5);
        var backtestConfig = CreateBacktestConfiguration();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            exporter.ExportTopStrategiesAsync(
                results, 
                backtestConfig, 
                _tempDirectory, 
                topCount: 0));
    }

    // Helper methods
    private MockTestStrategy CreateMockStrategy()
    {
        var strategy = new MockTestStrategy
        {
            Version = "1.0.0"
        };

        // Add a parameter
        var periodParam = new NumberParam<int>("Period", 20, 10, 50, 10)
        {
            CanOptimize = true
        };
        periodParam.Value = 20; // Direct assignment to Value property
        strategy.CustomParams.Add("Period", periodParam);

        // Add a security
        var security = new Security
        {
            Id = "BTCUSDT@CRYPTO",
            Code = "BTCUSDT",
            Board = new ExchangeBoard { Code = "CRYPTO" }
        };
        strategy.Securities.Add(security, new[] { TimeSpan.FromMinutes(5) });

        return strategy;
    }

    private OptimizationResult<MockTestStrategy> CreateOptimizationResult(MockTestStrategy strategy)
    {
        return new OptimizationResult<MockTestStrategy>
        {
            TrainedStrategy = strategy,
            Config = CreateOptimizationConfig(),
            StartTime = DateTimeOffset.UtcNow,
            TrainingMetrics = new PerformanceMetrics
            {
                NetProfit = 1000,
                SortinoRatio = 2.0
            },
            ValidationMetrics = new PerformanceMetrics
            {
                NetProfit = 800,
                SortinoRatio = 1.8
            }
        };
    }

    private OptimizationResult<MockTestStrategy> CreateOptimizationResultWithMetrics(decimal? sortinoRatio)
    {
        var strategy = CreateMockStrategy();
        return new OptimizationResult<MockTestStrategy>
        {
            TrainedStrategy = strategy,
            Config = CreateOptimizationConfig(),
            StartTime = DateTimeOffset.UtcNow,
            TrainingMetrics = new PerformanceMetrics { NetProfit = 1000 },
            ValidationMetrics = sortinoRatio.HasValue 
                ? new PerformanceMetrics { NetProfit = 800, SortinoRatio = (double)sortinoRatio.Value }
                : null
        };
    }

    private List<OptimizationResult<MockTestStrategy>> CreateMultipleResults(int count)
    {
        var results = new List<OptimizationResult<MockTestStrategy>>();
        for (int i = 0; i < count; i++)
        {
            var strategy = CreateMockStrategy();
            results.Add(new OptimizationResult<MockTestStrategy>
            {
                TrainedStrategy = strategy,
                Config = CreateOptimizationConfig(),
                StartTime = DateTimeOffset.UtcNow.AddHours(-i),
                TrainingMetrics = new PerformanceMetrics { NetProfit = 1000 + i * 100 },
                ValidationMetrics = new PerformanceMetrics 
                { 
                    NetProfit = 800 + i * 50,
                    SortinoRatio = 1.0 + i * 0.1
                }
            });
        }
        return results;
    }

    private OptimizationConfig CreateOptimizationConfig()
    {
        return new OptimizationConfig
        {
            ParamsContainer = new CustomParamsContainer(),
            TrainingPeriod = new OptimizationPeriodConfig
            {
                TrainingStartDate = DateTimeOffset.UtcNow.AddMonths(-6),
                TrainingEndDate = DateTimeOffset.UtcNow.AddMonths(-3),
                ValidationStartDate = DateTimeOffset.UtcNow.AddMonths(-3),
                ValidationEndDate = DateTimeOffset.UtcNow
            },
            HistoryPath = _tempDirectory
        };
    }

    private BacktestConfiguration CreateBacktestConfiguration()
    {
        return new BacktestConfiguration
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            InitialCapital = 10000m,
            TradeVolume = 100m,
            Securities = ["BTCUSDT"],
            TimeFrames = ["5m"],
            TrainingStartDate = DateTimeOffset.UtcNow.AddMonths(-6),
            TrainingEndDate = DateTimeOffset.UtcNow.AddMonths(-3),
            ValidationStartDate = DateTimeOffset.UtcNow.AddMonths(-3),
            ValidationEndDate = DateTimeOffset.UtcNow,
            HistoryPath = _tempDirectory,
            OptimizableParameters = new Dictionary<string, ParameterDefinition>()
        };
    }

    private StrategyParametersConfig CreateStrategyParametersConfig()
    {
        return new StrategyParametersConfig
        {
            StrategyName = "TestStrategy",
            StrategyVersion = "1.0.0",
            StrategyHash = "abc123",
            OptimizationDate = DateTimeOffset.UtcNow,
            InitialCapital = 10000m,
            TradeVolume = 100m,
            Parameters = new Dictionary<string, JsonElement>
            {
                ["Period"] = JsonSerializer.SerializeToElement(20)
            },
            Securities = ["BTCUSDT"],
            TrainingMetrics = new PerformanceMetrics { NetProfit = 1000 },
            ValidationMetrics = new PerformanceMetrics { NetProfit = 800 }
        };
    }
}
