using System.Text.Json;
using StockSharp.Algo;
using StockSharp.Algo.Candles;
using StockSharp.Algo.Commissions;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Testing;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration;
using StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;
using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Strategies.Modules;
using StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;
using StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;
using StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;
using StockSharp.AdvancedBacktest.PerformanceValidation;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Backtest;

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
            ConsoleLogger.LogInfo($"Run Mode: {_config.RunMode}");
            ConsoleLogger.LogInfo($"Training Period: {_config.TrainingStartDate:yyyy-MM-dd} to {_config.TrainingEndDate:yyyy-MM-dd}");
            ConsoleLogger.LogInfo($"Validation Period: {_config.ValidationStartDate:yyyy-MM-dd} to {_config.ValidationEndDate:yyyy-MM-dd}");

            // Route to appropriate execution path based on RunMode
            return _config.RunMode switch
            {
                Configuration.Models.RunMode.Optimization => await RunOptimizationModeAsync(),
                Configuration.Models.RunMode.Single => await RunSingleModeAsync(),
                _ => throw new InvalidOperationException($"Unsupported run mode: {_config.RunMode}")
            };
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

    private async Task<int> RunOptimizationModeAsync()
    {
        try
        {
            ConsoleLogger.LogSection("Running Optimization Mode");

            ValidateConfiguration();
            var paramContainer = BuildParameterContainer();
            var totalCombinations = CalculateTotalCombinations(paramContainer);
            ConsoleLogger.LogInfo($"Total parameter combinations: {totalCombinations:N0}");
            if (totalCombinations > 10000)
            {
                ConsoleLogger.LogWarning($"Large parameter space ({totalCombinations:N0} combinations) may take significant time");
            }

            var optimizationConfig = CreateOptimizationConfig(paramContainer);

            var optimizer = new OptimizerRunner<TStrategy>();
            optimizer.CreateOptimizer(optimizationConfig);

            ConsoleLogger.LogInfo($"Running optimization with {ParallelThreads} parallel workers");
            var results = optimizer.Optimize();

            if (results.Count == 0)
            {
                ConsoleLogger.LogWarning("No optimization results generated. Check parameter ranges and market data.");
                return 1;
            }

            ConsoleLogger.LogSuccess($"Optimization completed: {results.Count} strategy configurations evaluated");

            WalkForwardResult? walkForwardResult = null;
            if (_config.WalkForwardConfig != null)
            {
                walkForwardResult = await RunWalkForwardValidationAsync(optimizer, optimizationConfig);
                ConsoleLogger.LogSuccess($"Walk-forward validation completed: {walkForwardResult.TotalWindows} windows processed");
                ConsoleLogger.LogInfo($"Walk-Forward Efficiency: {walkForwardResult.WalkForwardEfficiency:F4}");
                ConsoleLogger.LogInfo($"Consistency (Std Dev): {walkForwardResult.Consistency:F4}");
            }

            await GenerateReportsAsync(results, optimizationConfig, walkForwardResult);
            await ExportTopStrategiesAsync(results);

            ConsoleLogger.LogSection("Optimization Complete");
            LogSummary(results, walkForwardResult);

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Optimization mode failed: {ex.Message}");
            if (VerboseLogging)
            {
                ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    private async Task<int> RunSingleModeAsync()
    {
        try
        {
            ConsoleLogger.LogSection("Running Single Mode");

            ValidateSingleModeConfiguration();
            var paramContainer = BuildParameterContainerFromFixed();
            var optimizationConfig = CreateOptimizationConfig(paramContainer);

            var optimizer = new OptimizerRunner<TStrategy>();
            optimizer.CreateOptimizer(optimizationConfig);

            ConsoleLogger.LogInfo("Running backtest on training and validation periods...");
            var results = optimizer.Optimize();

            if (results.Count == 0)
            {
                ConsoleLogger.LogWarning("Backtest failed to generate results.");
                return 1;
            }

            var result = results.Values.First();
            var trainingResult = result.TrainingMetrics;
            var validationResult = result.ValidationMetrics;

            ConsoleLogger.LogSuccess($"Training: Net Profit {trainingResult?.NetProfit:C2}");
            ConsoleLogger.LogSuccess($"Validation: Net Profit {validationResult?.NetProfit:C2}");

            await GenerateSingleModeReportAsync(trainingResult, validationResult);

            if (!string.IsNullOrWhiteSpace(_config.ExportPath))
            {
                await ExportSingleModeResultsAsync(result.TrainedStrategy!, result.ValidatedStrategy!, trainingResult, validationResult);
                ConsoleLogger.LogInfo($"Results exported to: {_config.ExportPath}");
            }

            ConsoleLogger.LogSection("Single Mode Complete");
            LogSingleModeSummary(trainingResult, validationResult);

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Single mode failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                ConsoleLogger.LogError($"Inner exception: {ex.InnerException.Message}");
                if (VerboseLogging && ex.InnerException.StackTrace != null)
                {
                    ConsoleLogger.LogError($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
            }
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

        ValidateHistoryPath();

        if (_config.OptimizableParameters == null || _config.OptimizableParameters.Count == 0)
        {
            throw new InvalidOperationException("At least one optimizable parameter must be specified in Optimization mode");
        }

        if (_config.Securities.Count == 0)
        {
            throw new InvalidOperationException("At least one security must be specified");
        }

        ValidateHistoryDataAccess();
    }

    private void ValidateHistoryPath()
    {
        if (string.IsNullOrWhiteSpace(_config.HistoryPath))
        {
            throw new ArgumentException("History path is required");
        }

        var isOneDrivePath = _config.HistoryPath.Contains("OneDrive", StringComparison.OrdinalIgnoreCase);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            if (Directory.Exists(_config.HistoryPath))
            {
                if (attempt > 1)
                {
                    ConsoleLogger.LogInfo($"History path accessible after {attempt} attempts");
                }
                return;
            }

            if (isOneDrivePath && attempt < 3)
            {
                ConsoleLogger.LogWarning($"OneDrive path not immediately accessible, waiting for sync (attempt {attempt}/3)...");
                Thread.Sleep(2000);
            }
        }

        var errorMessage = $"History path not found: {_config.HistoryPath}";
        if (isOneDrivePath)
        {
            errorMessage += "\n  Note: This appears to be a OneDrive path. Please ensure:" +
                          "\n  - OneDrive is running and synced" +
                          "\n  - The folder is available offline" +
                          "\n  - You have network connectivity";
        }

        throw new DirectoryNotFoundException(errorMessage);
    }

    private void ValidateHistoryDataAccess()
    {
        try
        {
            ConsoleLogger.LogInfo("Validating history data availability...");

            var validator = new HistoryDataValidator(_config.HistoryPath);
            var report = validator.Validate(_config.Securities, _config.TimeFrames);

            if (!report.IsSuccess)
            {
                ConsoleLogger.LogWarning("History data validation issues detected:");
                foreach (var error in report.Errors)
                {
                    ConsoleLogger.LogError($"  - {error}");
                }

                foreach (var warning in report.Warnings)
                {
                    ConsoleLogger.LogWarning($"  - {warning}");
                }

                if (report.Errors.Count > 0)
                {
                    throw new InvalidOperationException("History data validation failed. Check errors above.");
                }
            }
            else
            {
                ConsoleLogger.LogSuccess($"History data validated: {report.SecurityResults.Count} securities available");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to validate history data: {ex.Message}", ex);
        }
    }

    private CustomParamsContainer BuildParameterContainer()
    {
        var parameters = new List<ICustomParam>();

        AddSecurityParameters(parameters);

        if (_config.OptimizableParameters != null)
        {
            parameters.AddRange(
                ParameterFactory.CreateFromDictionary(_config.OptimizableParameters));
        }

        return new CustomParamsContainer(parameters);
    }

    private void AddSecurityParameters(List<ICustomParam> customParams)
    {
        if (_config.Securities.Count == 0)
        {
            throw new InvalidOperationException("At least one security must be specified");
        }

        if (_config.TimeFrames.Count == 0)
        {
            throw new InvalidOperationException("At least one timeframe must be specified");
        }

        var securityTimeframes = new List<SecurityTimeframes>();

        foreach (var securityId in _config.Securities)
        {
            var security = new Security { Id = securityId };
            securityTimeframes.Add(new SecurityTimeframes(security, _config.TimeFrames));
        }

        var securityParam = new SecurityParam("Security", securityTimeframes)
        {
            CanOptimize = true
        };

        customParams.Add(securityParam);
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
        var trainingPeriod = new PeriodConfig
        {
            StartDate = _config.TrainingStartDate,
            EndDate = _config.TrainingEndDate
        };

        var validationPeriod = new PeriodConfig
        {
            StartDate = _config.ValidationStartDate,
            EndDate = _config.ValidationEndDate
        };

        return new OptimizationConfig
        {
            ParamsContainer = paramContainer,
            TrainingPeriod = trainingPeriod,
            ValidationPeriod = validationPeriod,
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

        filters.Add(metrics => metrics.TotalTrades >= 10);
        filters.Add(metrics => metrics.NetProfit > 0);

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

        ConsoleLogger.LogInfo($"Mode: {wfConfig.Mode}, Window: {wfConfig.WindowSize.TotalDays}d, Step: {wfConfig.StepSize.TotalDays}d, Validation: {wfConfig.ValidationSize.TotalDays}d");

        var result = validator.Validate(wfConfig, startDate, endDate);

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
            await writer.WriteLineAsync($"Mode: {_config.WalkForwardConfig!.Mode}");
            await writer.WriteLineAsync($"Total Windows: {walkForwardResult.TotalWindows}");
            await writer.WriteLineAsync($"WF Efficiency: {walkForwardResult.WalkForwardEfficiency:F4}");
            await writer.WriteLineAsync($"Consistency (Std Dev): {walkForwardResult.Consistency:F4}");
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

        var exporter = new StrategyExporter<TStrategy>();
        var exportedPaths = await exporter.ExportTopStrategiesAsync(
            results.Values,
            _config,
            _config.ExportPath,
            topCount: 5,
            verboseLogging: VerboseLogging);

        ConsoleLogger.LogInfo($"Exported {exportedPaths.Count} top strategies to: {_config.ExportPath}");
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
            ConsoleLogger.LogInfo($"  Consistency (Std Dev): {walkForwardResult.Consistency:F4}");
        }

        ConsoleLogger.LogInfo($"\nResults saved to: {OutputDirectory}");
    }

    private void ValidateSingleModeConfiguration()
    {
        if (_config.TrainingEndDate <= _config.TrainingStartDate)
        {
            throw new InvalidOperationException("Training end date must be after training start date");
        }

        if (_config.ValidationEndDate <= _config.ValidationStartDate)
        {
            throw new InvalidOperationException("Validation end date must be after validation start date");
        }

        ValidateHistoryPath();

        if (_config.FixedParameters == null || _config.FixedParameters.Count == 0)
        {
            throw new InvalidOperationException("Single mode requires at least one fixed parameter");
        }

        if (_config.Securities.Count == 0)
        {
            throw new InvalidOperationException("At least one security must be specified");
        }

        ValidateHistoryDataAccess();
    }

    private CustomParamsContainer BuildParameterContainerFromFixed()
    {
        var parameters = new List<ICustomParam>();

        AddSecurityParameters(parameters);

        foreach (var fixedParam in _config.FixedParameters)
        {
            var paramName = fixedParam.Key;
            var paramValue = fixedParam.Value;

            ICustomParam param = InferParameterTypeAndCreate(paramName, paramValue);
            parameters.Add(param);
        }

        return new CustomParamsContainer(parameters);
    }

    private ICustomParam InferParameterTypeAndCreate(string name, JsonElement value)
    {
        if (name.EndsWith(".Type") && name.StartsWith("TrendFilter."))
        {
            var enumValue = Enum.Parse<IndicatorType>(value.GetString() ?? string.Empty);
            return new StructParam<IndicatorType>(name, [enumValue]) { CanOptimize = true };
        }

        if (name.EndsWith(".Method"))
        {
            if (name.StartsWith("PositionSizing."))
            {
                var enumValue = Enum.Parse<PositionSizingMethod>(value.GetString() ?? string.Empty);
                return new StructParam<PositionSizingMethod>(name, [enumValue]) { CanOptimize = true };
            }
            else if (name.StartsWith("StopLoss."))
            {
                var enumValue = Enum.Parse<StopLossMethod>(value.GetString() ?? string.Empty);
                return new StructParam<StopLossMethod>(name, [enumValue]) { CanOptimize = true };
            }
            else if (name.StartsWith("TakeProfit."))
            {
                var enumValue = Enum.Parse<TakeProfitMethod>(value.GetString() ?? string.Empty);
                return new StructParam<TakeProfitMethod>(name, [enumValue]) { CanOptimize = true };
            }
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                if (value.TryGetInt32(out int intValue))
                {
                    return new NumberParam<int>(name, intValue, intValue, intValue, 1) { CanOptimize = true };
                }
                else if (value.TryGetDecimal(out decimal decimalValue))
                {
                    return new NumberParam<decimal>(name, decimalValue, decimalValue, decimalValue, 0.01m) { CanOptimize = true };
                }
                else if (value.TryGetDouble(out double doubleValue))
                {
                    return new NumberParam<double>(name, doubleValue, doubleValue, doubleValue, 0.01) { CanOptimize = true };
                }
                break;

            case JsonValueKind.String:
                string stringValue = value.GetString() ?? string.Empty;
                return new ClassParam<string>(name, [stringValue]) { CanOptimize = true };

            case JsonValueKind.True:
            case JsonValueKind.False:
                bool boolValue = value.GetBoolean();
                return new ClassParam<string>(name, [boolValue.ToString()]) { CanOptimize = true };
        }

        throw new InvalidOperationException($"Unable to infer parameter type for '{name}' with value kind: {value.ValueKind}");
    }

    private async Task GenerateSingleModeReportAsync(PerformanceMetrics? trainingMetrics, PerformanceMetrics? validationMetrics)
    {
        Directory.CreateDirectory(OutputDirectory);

        var reportPath = Path.Combine(OutputDirectory, "single_run_report.txt");
        await using var writer = new StreamWriter(reportPath);

        await writer.WriteLineAsync("=== Single Run Mode Report ===");
        await writer.WriteLineAsync($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        await writer.WriteLineAsync($"Strategy: {_config.StrategyName} v{_config.StrategyVersion}");
        await writer.WriteLineAsync();

        await writer.WriteLineAsync("Fixed Parameters:");
        foreach (var param in _config.FixedParameters)
        {
            await writer.WriteLineAsync($"  {param.Key}: {param.Value}");
        }
        await writer.WriteLineAsync();

        if (trainingMetrics != null)
        {
            await writer.WriteLineAsync("=== Training Period Results ===");
            await writer.WriteLineAsync($"Period: {_config.TrainingStartDate:yyyy-MM-dd} to {_config.TrainingEndDate:yyyy-MM-dd}");
            await writer.WriteLineAsync($"Net Profit: {trainingMetrics.NetProfit:C2}");
            await writer.WriteLineAsync($"Total Return: {trainingMetrics.TotalReturn:P2}");
            await writer.WriteLineAsync($"Sortino Ratio: {trainingMetrics.SortinoRatio:F4}");
            await writer.WriteLineAsync($"Win Rate: {trainingMetrics.WinRate:P2}");
            await writer.WriteLineAsync($"Total Trades: {trainingMetrics.TotalTrades}");
            await writer.WriteLineAsync($"Max Drawdown: {trainingMetrics.MaxDrawdown:P2}");
            await writer.WriteLineAsync();
        }

        if (validationMetrics != null)
        {
            await writer.WriteLineAsync("=== Validation Period Results ===");
            await writer.WriteLineAsync($"Period: {_config.ValidationStartDate:yyyy-MM-dd} to {_config.ValidationEndDate:yyyy-MM-dd}");
            await writer.WriteLineAsync($"Net Profit: {validationMetrics.NetProfit:C2}");
            await writer.WriteLineAsync($"Total Return: {validationMetrics.TotalReturn:P2}");
            await writer.WriteLineAsync($"Sortino Ratio: {validationMetrics.SortinoRatio:F4}");
            await writer.WriteLineAsync($"Win Rate: {validationMetrics.WinRate:P2}");
            await writer.WriteLineAsync($"Total Trades: {validationMetrics.TotalTrades}");
            await writer.WriteLineAsync($"Max Drawdown: {validationMetrics.MaxDrawdown:P2}");
        }

        ConsoleLogger.LogInfo($"Report saved to: {reportPath}");
    }

    private async Task ExportSingleModeResultsAsync(
        TStrategy trainingStrategy,
        TStrategy validationStrategy,
        PerformanceMetrics? trainingMetrics,
        PerformanceMetrics? validationMetrics)
    {
        var exportDir = _config.ExportPath!;
        Directory.CreateDirectory(exportDir);

        // Export as JSON
        var exportData = new
        {
            Strategy = new
            {
                Name = _config.StrategyName,
                Version = _config.StrategyVersion,
                Description = _config.StrategyDescription
            },
            RunMode = "Single",
            Parameters = _config.FixedParameters,
            Training = new
            {
                Period = new
                {
                    Start = _config.TrainingStartDate,
                    End = _config.TrainingEndDate
                },
                Metrics = trainingMetrics
            },
            Validation = new
            {
                Period = new
                {
                    Start = _config.ValidationStartDate,
                    End = _config.ValidationEndDate
                },
                Metrics = validationMetrics
            },
            ExportedAt = DateTimeOffset.UtcNow
        };

        var jsonPath = Path.Combine(exportDir, "single_run_results.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonContent = JsonSerializer.Serialize(exportData, jsonOptions);
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        ConsoleLogger.LogInfo($"Results exported to: {jsonPath}");
    }

    private void LogSingleModeSummary(PerformanceMetrics? trainingMetrics, PerformanceMetrics? validationMetrics)
    {
        ConsoleLogger.LogSuccess("✓ Single mode completed successfully");
        ConsoleLogger.LogInfo($"  Fixed Parameters: {_config.FixedParameters.Count}");

        if (trainingMetrics != null)
        {
            ConsoleLogger.LogInfo($"  Training Net Profit: {trainingMetrics.NetProfit:C2}");
            ConsoleLogger.LogInfo($"  Training Sortino Ratio: {trainingMetrics.SortinoRatio:F4}");
            ConsoleLogger.LogInfo($"  Training Total Trades: {trainingMetrics.TotalTrades}");
        }

        if (validationMetrics != null)
        {
            ConsoleLogger.LogInfo($"  Validation Net Profit: {validationMetrics.NetProfit:C2}");
            ConsoleLogger.LogInfo($"  Validation Sortino Ratio: {validationMetrics.SortinoRatio:F4}");
            ConsoleLogger.LogInfo($"  Validation Total Trades: {validationMetrics.TotalTrades}");
        }

        ConsoleLogger.LogInfo($"\nResults saved to: {OutputDirectory}");
    }
}
