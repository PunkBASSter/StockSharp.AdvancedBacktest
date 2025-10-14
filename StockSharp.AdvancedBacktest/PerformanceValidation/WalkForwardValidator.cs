using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.PerformanceValidation;

public class WalkForwardValidator<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    private readonly OptimizerRunner<TStrategy> _optimizerRunner;
    private readonly OptimizationConfig _baseConfig;
    private readonly Func<OptimizationConfig, Dictionary<string, OptimizationResult<TStrategy>>>? _optimizeFunc;

    public WalkForwardValidator(OptimizerRunner<TStrategy> runner, OptimizationConfig baseConfig)
    {
        _optimizerRunner = runner ?? throw new ArgumentNullException(nameof(runner));
        _baseConfig = baseConfig ?? throw new ArgumentNullException(nameof(baseConfig));
        _optimizeFunc = null;
    }

    public WalkForwardValidator(
        OptimizerRunner<TStrategy> runner,
        OptimizationConfig baseConfig,
        Func<OptimizationConfig, Dictionary<string, OptimizationResult<TStrategy>>> optimizeFunc)
    {
        _optimizerRunner = runner!;
        _baseConfig = baseConfig ?? throw new ArgumentNullException(nameof(baseConfig));
        _optimizeFunc = optimizeFunc ?? throw new ArgumentNullException(nameof(optimizeFunc));
    }

    public WalkForwardResult Validate(WalkForwardConfig wfConfig, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        if (wfConfig == null)
            throw new ArgumentNullException(nameof(wfConfig));

        var windows = wfConfig.GenerateWindows(startDate, endDate).ToList();

        if (windows.Count == 0)
        {
            return new WalkForwardResult
            {
                TotalWindows = 0,
                Windows = []
            };
        }

        var windowResults = new List<WindowResult>();
        var windowNumber = 1;

        foreach (var (trainStart, trainEnd, testStart, testEnd) in windows)
        {
            try
            {
                var tempConfig = CloneConfigWithNewPeriod(trainStart, trainEnd, testStart, testEnd);

                Dictionary<string, OptimizationResult<TStrategy>> optimizationResults;

                if (_optimizeFunc != null)
                {
                    optimizationResults = _optimizeFunc(tempConfig);
                }
                else
                {
                    _optimizerRunner.CreateOptimizer(tempConfig);
                    optimizationResults = _optimizerRunner.Optimize();
                }

                if (optimizationResults.Count == 0)
                {
                    Console.WriteLine($"Window {windowNumber}: No optimization results found. Skipping window.");
                    windowNumber++;
                    continue;
                }

                var bestStrategy = SelectBestStrategy(optimizationResults);

                if (bestStrategy.TrainingMetrics == null || bestStrategy.ValidationMetrics == null)
                {
                    Console.WriteLine($"Window {windowNumber}: Missing metrics for best strategy. Skipping window.");
                    windowNumber++;
                    continue;
                }

                var windowResult = new WindowResult
                {
                    WindowNumber = windowNumber,
                    TrainingMetrics = bestStrategy.TrainingMetrics,
                    TestingMetrics = bestStrategy.ValidationMetrics,
                    TrainingPeriod = (trainStart, trainEnd),
                    TestingPeriod = (testStart, testEnd)
                };

                windowResults.Add(windowResult);
                Console.WriteLine($"Window {windowNumber} completed: Train Return={bestStrategy.TrainingMetrics.TotalReturn:F2}%, Test Return={bestStrategy.ValidationMetrics.TotalReturn:F2}%");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Window {windowNumber} failed with error: {ex.Message}");
            }

            windowNumber++;
        }

        return new WalkForwardResult
        {
            TotalWindows = windowResults.Count,
            Windows = windowResults
        };
    }

    private OptimizationConfig CloneConfigWithNewPeriod(
        DateTimeOffset trainStart,
        DateTimeOffset trainEnd,
        DateTimeOffset testStart,
        DateTimeOffset testEnd)
    {
        var trainingPeriodConfig = new PeriodConfig
        {
            StartDate = trainStart,
            EndDate = trainEnd,
        };

        var validationPeriodConfig = new PeriodConfig
        {
            StartDate = testStart,
            EndDate = testEnd
        };

        return new OptimizationConfig
        {
            ParamsContainer = _baseConfig.ParamsContainer,
            TrainingPeriod = trainingPeriodConfig,
            ValidationPeriod = validationPeriodConfig,
            MetricFilters = _baseConfig.MetricFilters,
            InitialCapital = _baseConfig.InitialCapital,
            CommissionRules = _baseConfig.CommissionRules,
            TradeVolume = _baseConfig.TradeVolume,
            IsBruteForce = _baseConfig.IsBruteForce,
            ParallelWorkers = _baseConfig.ParallelWorkers,
            HistoryPath = _baseConfig.HistoryPath,
            GeneticSettings = _baseConfig.GeneticSettings
        };
    }

    private OptimizationResult<TStrategy> SelectBestStrategy(
        Dictionary<string, OptimizationResult<TStrategy>> optimizationResults)
    {
        var bestStrategy = optimizationResults.Values
            .Where(r => r.TrainingMetrics != null)
            .OrderByDescending(r => r.TrainingMetrics!.SharpeRatio)
            .ThenByDescending(r => r.TrainingMetrics!.SortinoRatio)
            .ThenByDescending(r => r.TrainingMetrics!.TotalReturn)
            .First();

        return bestStrategy;
    }
}
