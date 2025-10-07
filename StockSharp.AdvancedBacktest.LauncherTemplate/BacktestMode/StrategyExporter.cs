using System.Text.Json;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

public class StrategyExporter<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    private readonly JsonSerializerOptions _jsonOptions;

    public StrategyExporter()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public StrategyParametersConfig BuildConfiguration(
        OptimizationResult<TStrategy> result,
        BacktestConfiguration backtestConfig)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(backtestConfig);
        ArgumentNullException.ThrowIfNull(result.TrainedStrategy);

        var strategy = result.TrainedStrategy;

        var config = new StrategyParametersConfig
        {
            StrategyName = backtestConfig.StrategyName,
            StrategyVersion = strategy.Version,
            StrategyHash = GenerateConfigHash(strategy),
            OptimizationDate = result.StartTime,
            Parameters = ExtractParameters(strategy),
            InitialCapital = backtestConfig.InitialCapital,
            TradeVolume = backtestConfig.TradeVolume,
            Securities = ExtractSecurities(strategy),
            TrainingMetrics = result.TrainingMetrics,
            ValidationMetrics = result.ValidationMetrics,
            WalkForwardMetrics = null // Walk-forward results don't have a single aggregate metric
        };

        return config;
    }

    public async Task ExportAsync(StrategyParametersConfig config, string filePath)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<List<string>> ExportTopStrategiesAsync(
        IEnumerable<OptimizationResult<TStrategy>> results,
        BacktestConfiguration backtestConfig,
        string outputDirectory,
        int topCount = 5,
        bool verboseLogging = false)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(backtestConfig);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty", nameof(outputDirectory));
        }

        if (topCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topCount), "Top count must be greater than 0");
        }

        Directory.CreateDirectory(outputDirectory);

        var topStrategies = results
            .Where(r => r.ValidationMetrics != null)
            .OrderByDescending(r => r.ValidationMetrics!.SortinoRatio)
            .Take(topCount)
            .ToList();

        var exportedPaths = new List<string>();

        for (int i = 0; i < topStrategies.Count; i++)
        {
            var result = topStrategies[i];
            var fileName = $"strategy_{i + 1}.json";
            var filePath = Path.Combine(outputDirectory, fileName);

            var config = BuildConfiguration(result, backtestConfig);
            await ExportAsync(config, filePath);

            exportedPaths.Add(filePath);

            if (verboseLogging)
            {
                ConsoleLogger.LogInfo($"Exported strategy #{i + 1} to: {filePath}");
                ConsoleLogger.LogInfo($"  Sortino Ratio: {result.ValidationMetrics?.SortinoRatio:F4}");
                ConsoleLogger.LogInfo($"  Net Profit: {result.ValidationMetrics?.NetProfit:C2}");
            }
        }

        return exportedPaths;
    }

    private Dictionary<string, JsonElement> ExtractParameters(CustomStrategyBase strategy)
    {
        var parameters = new Dictionary<string, JsonElement>();

        foreach (var param in strategy.CustomParams)
        {
            var value = param.Value.Value;
            parameters[param.Key] = JsonSerializer.SerializeToElement(value);
        }

        return parameters;
    }

    private List<string> ExtractSecurities(CustomStrategyBase strategy)
    {
        return strategy.Securities.Keys
            .Select(s => s.Id)
            .ToList();
    }

    private string GenerateConfigHash(CustomStrategyBase strategy)
    {
        var hashInput = $"{strategy.GetType().Name}_{strategy.Version}_{strategy.ParamsHash}_{strategy.SecuritiesHash}";
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes)[..32];
    }
}
