using System.Text.Json;
using StockSharp.Algo.Commissions;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Validation;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

public class BacktestRunner<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    private readonly BacktestConfiguration _config;

    public string OutputDirectory { get; set; } = "./output";
    public int ParallelThreads { get; set; }
    public bool VerboseLogging { get; set; }

    public BacktestRunner(BacktestConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        ParallelThreads = config.ParallelWorkers;
    }

    public async Task<int> RunAsync()
    {
        try
        {
            ConsoleLogger.LogSection("Starting Backtest Workflow");
            ConsoleLogger.LogInfo($"Strategy: {_config.StrategyName} v{_config.StrategyVersion}");
            ConsoleLogger.LogInfo($"Training Period: {_config.TrainingStartDate:yyyy-MM-dd} to {_config.TrainingEndDate:yyyy-MM-dd}");
            ConsoleLogger.LogInfo($"Validation Period: {_config.ValidationStartDate:yyyy-MM-dd} to {_config.ValidationEndDate:yyyy-MM-dd}");

            // Step 1: Validate configuration
            ConsoleLogger.LogSection("Step 1: Validating Configuration");
            ValidateConfiguration();
            ConsoleLogger.LogSuccess("Configuration validated successfully");

            // Step 2: Build parameter container
            ConsoleLogger.LogSection("Step 2: Building Parameter Container");
            var paramContainer = BuildParameterContainer();
            var totalCombinations = CalculateTotalCombinations(paramContainer);
            ConsoleLogger.LogInfo($"Total parameter combinations: {totalCombinations:N0}");
            if (totalCombinations > 10000)
            {
                ConsoleLogger.LogWarning($"Large parameter space ({totalCombinations:N0} combinations) may take significant time");
            }

            // Step 3: Create optimization config
            ConsoleLogger.LogSection("Step 3: Creating Optimization Configuration");
            var optimizationConfig = CreateOptimizationConfig(paramContainer);
            ConsoleLogger.LogSuccess("Optimization configuration created");

            // Step 4: Execute optimization
            ConsoleLogger.LogSection("Step 4: Executing Optimization");
            var optimizer = new OptimizerRunner<TStrategy>();
            var baseOptimizer = optimizer.CreateOptimizer(optimizationConfig);

            ConsoleLogger.LogInfo($"Running optimization with {ParallelThreads} parallel workers");
            var results = optimizer.Optimize();

            if (results.Count == 0)
            {
                ConsoleLogger.LogWarning("No optimization results generated. Check parameter ranges and market data.");
                return 1;
            }

            ConsoleLogger.LogSuccess($"Optimization completed: {results.Count} strategy configurations evaluated");

            // Step 5: Run walk-forward validation (if enabled)
            WalkForwardResult? walkForwardResult = null;
            if (_config.WalkForwardConfig != null)
            {
                ConsoleLogger.LogSection("Step 5: Running Walk-Forward Validation");
                walkForwardResult = await RunWalkForwardValidationAsync(optimizer, optimizationConfig);
                ConsoleLogger.LogSuccess($"Walk-forward validation completed: {walkForwardResult.TotalWindows} windows processed");
                ConsoleLogger.LogInfo($"Walk-Forward Efficiency: {walkForwardResult.WalkForwardEfficiency:F4}");
            }
            else
            {
                ConsoleLogger.LogInfo("Step 5: Walk-forward validation disabled (skipped)");
            }

            // Step 6: Generate reports
            ConsoleLogger.LogSection("Step 6: Generating Reports");
            await GenerateReportsAsync(results, optimizationConfig, walkForwardResult);
            ConsoleLogger.LogSuccess("Reports generated successfully");

            // Step 7: Export top strategies
            ConsoleLogger.LogSection("Step 7: Exporting Top Strategies");
            await ExportTopStrategiesAsync(results);
            ConsoleLogger.LogSuccess("Top strategies exported");

            // Step 8: Log summary
            ConsoleLogger.LogSection("Backtest Workflow Complete");
            LogSummary(results, walkForwardResult);

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Backtest workflow failed: {ex.Message}");
            if (VerboseLogging)
            {
                ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    private void ValidateConfiguration()
    {
        if (_config.TrainingEndDate <= _config.TrainingStartDate)
        {
            throw new InvalidOperationException("Training end date must be after training start date");
        }

        if (_config.ValidationEndDate <= _config.ValidationStartDate)
        {
            throw new InvalidOperationException("Validation end date must be after validation start date");
        }

        if (!Directory.Exists(_config.HistoryPath))
        {
            throw new DirectoryNotFoundException($"History path not found: {_config.HistoryPath}");
        }

        if (_config.OptimizableParameters.Count == 0)
        {
            throw new InvalidOperationException("At least one optimizable parameter must be specified");
        }

        if (_config.Securities.Count == 0)
        {
            throw new InvalidOperationException("At least one security must be specified");
        }

        if (VerboseLogging)
        {
            ConsoleLogger.LogInfo($"Configuration validation passed:");
            ConsoleLogger.LogInfo($"  - Parameters: {_config.OptimizableParameters.Count}");
            ConsoleLogger.LogInfo($"  - Securities: {string.Join(", ", _config.Securities)}");
            ConsoleLogger.LogInfo($"  - Parallel workers: {ParallelThreads}");
        }
    }

    private CustomParamsContainer BuildParameterContainer()
    {
        var customParams = new List<ICustomParam>();

        foreach (var (key, paramDef) in _config.OptimizableParameters)
        {
            try
            {
                var param = CreateParameterFromDefinition(key, paramDef);
                customParams.Add(param);

                if (VerboseLogging)
                {
                    ConsoleLogger.LogInfo($"  - {key} ({paramDef.Type}): {paramDef.MinValue} to {paramDef.MaxValue} step {paramDef.StepValue}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create parameter '{key}': {ex.Message}", ex);
            }
        }

        return new CustomParamsContainer { CustomParams = customParams };
    }

    private ICustomParam CreateParameterFromDefinition(string name, ParameterDefinition def)
    {
        return def.Type.ToLowerInvariant() switch
        {
            "int" => new NumberParam<int>(
                name,
                def.DefaultValue?.Deserialize<int>() ?? def.MinValue.Deserialize<int>(),
                def.MinValue.Deserialize<int>(),
                def.MaxValue.Deserialize<int>(),
                def.StepValue.Deserialize<int>())
            {
                CanOptimize = true
            },

            "decimal" => new NumberParam<decimal>(
                name,
                def.DefaultValue?.Deserialize<decimal>() ?? def.MinValue.Deserialize<decimal>(),
                def.MinValue.Deserialize<decimal>(),
                def.MaxValue.Deserialize<decimal>(),
                def.StepValue.Deserialize<decimal>())
            {
                CanOptimize = true
            },

            "double" => new NumberParam<double>(
                name,
                def.DefaultValue?.Deserialize<double>() ?? def.MinValue.Deserialize<double>(),
                def.MinValue.Deserialize<double>(),
                def.MaxValue.Deserialize<double>(),
                def.StepValue.Deserialize<double>())
            {
                CanOptimize = true
            },

            _ => throw new NotSupportedException($"Parameter type '{def.Type}' is not supported. Supported types: int, decimal, double")
        };
    }

    private long CalculateTotalCombinations(CustomParamsContainer container)
    {
        long total = 1;
        foreach (var param in container.CustomParams.Where(p => p.CanOptimize))
        {
            var rangeCount = param.OptimizationRangeParams.Count();
            total *= rangeCount > 0 ? rangeCount : 1;

            if (total > long.MaxValue / 2)
            {
                return long.MaxValue;
            }
        }
        return total;
    }

    private OptimizationConfig CreateOptimizationConfig(CustomParamsContainer paramContainer)
    {
        return new OptimizationConfig
        {
            ParamsContainer = paramContainer,
            TrainingPeriod = new OptimizationPeriodConfig
            {
                TrainingStartDate = _config.TrainingStartDate,
                TrainingEndDate = _config.TrainingEndDate,
                ValidationStartDate = _config.ValidationStartDate,
                ValidationEndDate = _config.ValidationEndDate
            },
            HistoryPath = _config.HistoryPath,
            InitialCapital = _config.InitialCapital,
            TradeVolume = _config.TradeVolume,
            CommissionRules = BuildCommissionRules(),
            MetricFilters = BuildMetricFilters(),
            IsBruteForce = _config.UseBruteForceOptimization,
            ParallelWorkers = ParallelThreads
        };
    }

    private List<ICommissionRule> BuildCommissionRules()
    {
        return
        [
            new CommissionTradeRule
            {
                Value = new Unit(_config.CommissionPercentage, UnitTypes.Percent)
            }
        ];
    }

    private List<Func<PerformanceMetrics, bool>> BuildMetricFilters()
    {
        var filters = new List<Func<PerformanceMetrics, bool>>();

        // Default filters - minimum trade count and positive return
        filters.Add(metrics => metrics.TotalTrades >= 10);
        filters.Add(metrics => metrics.NetProfit > 0);

        // TODO: Parse MetricFilterExpressions from config and convert to lambda expressions
        // This would require a simple expression parser or using Dynamic LINQ
        // For MVP, we use hardcoded reasonable defaults

        if (VerboseLogging)
        {
            ConsoleLogger.LogInfo($"Applied {filters.Count} metric filters");
        }

        return filters;
    }

    private async Task<WalkForwardResult> RunWalkForwardValidationAsync(
        OptimizerRunner<TStrategy> optimizer,
        OptimizationConfig config)
    {
        if (_config.WalkForwardConfig == null)
        {
            throw new InvalidOperationException("Walk-forward configuration is not set");
        }

        var wfConfig = _config.WalkForwardConfig;
        var validator = new WalkForwardValidator<TStrategy>(optimizer, config);
        var startDate = _config.TrainingStartDate;
        var endDate = _config.ValidationEndDate;

        var result = validator.Validate(wfConfig, startDate, endDate);

        if (VerboseLogging)
        {
            ConsoleLogger.LogInfo($"Walk-forward windows processed:");
            for (int i = 0; i < result.Windows.Count; i++)
            {
                var window = result.Windows[i];
                ConsoleLogger.LogInfo($"  Window {i + 1}: Training Return = {window.TrainingMetrics.TotalReturn:F4}, Testing Return = {window.TestingMetrics.TotalReturn:F4}");
            }
        }

        return await Task.FromResult(result);
    }

    private async Task GenerateReportsAsync(
        Dictionary<string, OptimizationResult<TStrategy>> results,
        OptimizationConfig config,
        WalkForwardResult? walkForwardResult)
    {
        Directory.CreateDirectory(OutputDirectory);

        var reportPath = Path.Combine(OutputDirectory, "optimization_report.txt");
        await using var writer = new StreamWriter(reportPath);

        await writer.WriteLineAsync("=== Optimization Report ===");
        await writer.WriteLineAsync($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        await writer.WriteLineAsync($"Strategy: {_config.StrategyName} v{_config.StrategyVersion}");
        await writer.WriteLineAsync();

        await writer.WriteLineAsync($"Total Configurations Tested: {results.Count}");

        var topResults = results
            .Where(r => r.Value.ValidationMetrics != null)
            .OrderByDescending(r => r.Value.ValidationMetrics!.SortinoRatio)
            .Take(10)
            .ToList();

        await writer.WriteLineAsync($"\nTop 10 Strategies (by Sortino Ratio):");
        for (int i = 0; i < topResults.Count; i++)
        {
            var result = topResults[i];
            await writer.WriteLineAsync($"\n{i + 1}. Configuration: {result.Key}");
            await writer.WriteLineAsync($"   Sortino Ratio: {result.Value.ValidationMetrics?.SortinoRatio:F4}");
            await writer.WriteLineAsync($"   Net Profit: {result.Value.ValidationMetrics?.NetProfit:C2}");
            await writer.WriteLineAsync($"   Win Rate: {result.Value.ValidationMetrics?.WinRate:P2}");
            await writer.WriteLineAsync($"   Total Trades: {result.Value.ValidationMetrics?.TotalTrades}");
        }

        if (walkForwardResult != null)
        {
            await writer.WriteLineAsync($"\n=== Walk-Forward Analysis ===");
            await writer.WriteLineAsync($"Total Windows: {walkForwardResult.TotalWindows}");
            await writer.WriteLineAsync($"WF Efficiency: {walkForwardResult.WalkForwardEfficiency:F4}");
        }

        ConsoleLogger.LogInfo($"Report saved to: {reportPath}");
    }

    private async Task ExportTopStrategiesAsync(Dictionary<string, OptimizationResult<TStrategy>> results)
    {
        if (string.IsNullOrWhiteSpace(_config.ExportPath))
        {
            ConsoleLogger.LogInfo("Export path not configured, skipping strategy export");
            return;
        }

        Directory.CreateDirectory(_config.ExportPath);

        var topStrategies = results
            .Where(r => r.Value.ValidationMetrics != null)
            .OrderByDescending(r => r.Value.ValidationMetrics!.SortinoRatio)
            .Take(5)
            .ToList();

        for (int i = 0; i < topStrategies.Count; i++)
        {
            var result = topStrategies[i];
            var exportPath = Path.Combine(_config.ExportPath, $"strategy_{i + 1}.json");

            var strategyConfig = new StrategyParametersConfig
            {
                StrategyName = _config.StrategyName,
                StrategyVersion = _config.StrategyVersion,
                StrategyHash = GenerateConfigHash(result.Key),
                OptimizationDate = DateTimeOffset.UtcNow,
                Parameters = new Dictionary<string, JsonElement>(),
                InitialCapital = _config.InitialCapital,
                TradeVolume = _config.TradeVolume,
                Securities = _config.Securities
            };

            // TODO: Extract actual parameter values from result.Key
            // For MVP, we save the configuration key as a reference
            strategyConfig.Parameters["ConfigurationKey"] = JsonSerializer.SerializeToElement(result.Key);

            var json = JsonSerializer.Serialize(strategyConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json);

            if (VerboseLogging)
            {
                ConsoleLogger.LogInfo($"Exported strategy #{i + 1} to: {exportPath}");
            }
        }

        ConsoleLogger.LogInfo($"Exported {topStrategies.Count} top strategies to: {_config.ExportPath}");
    }

    private void LogSummary(
        Dictionary<string, OptimizationResult<TStrategy>> results,
        WalkForwardResult? walkForwardResult)
    {
        var successfulResults = results.Where(r => r.Value.ValidationMetrics != null).ToList();
        var bestResult = successfulResults
            .OrderByDescending(r => r.Value.ValidationMetrics!.SortinoRatio)
            .FirstOrDefault();

        ConsoleLogger.LogSuccess($"✓ Workflow completed successfully");
        ConsoleLogger.LogInfo($"  Total configurations: {results.Count}");
        ConsoleLogger.LogInfo($"  Successful runs: {successfulResults.Count}");

        if (bestResult.Value != null)
        {
            ConsoleLogger.LogInfo($"  Best Sortino Ratio: {bestResult.Value.ValidationMetrics?.SortinoRatio:F4}");
            ConsoleLogger.LogInfo($"  Best Net Profit: {bestResult.Value.ValidationMetrics?.NetProfit:C2}");
        }

        if (walkForwardResult != null)
        {
            ConsoleLogger.LogInfo($"  WF Efficiency: {walkForwardResult.WalkForwardEfficiency:F4}");
        }

        ConsoleLogger.LogInfo($"\nResults saved to: {OutputDirectory}");
    }

    private string GenerateConfigHash(string configKey)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(configKey));
        return Convert.ToHexString(hashBytes)[..32];
    }
}
