using Ecng.Logging;
using StockSharp.Algo.Commissions;
using StockSharp.Algo.Strategies.Optimization;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.Optimization;


/// <summary>
/// Most likely broken now, needs to be decomposed into smaller parts.
/// </summary>
public class OptimizationLauncher<TStrategy> : LauncherBase<TStrategy>
    where TStrategy : CustomStrategyBase, new()
{
    private readonly PeriodConfig _trainingPeriod;
    private List<ICommissionRule> _commissionRules = new();
    private WalkForwardConfig? _walkForwardConfig;
    private WalkForwardResult? _walkForwardResult;
    public decimal InitialCapital { get; set; } = 10000m;
    private BaseOptimizer? _stockSharpOptimizer;
    private bool _bruteForce = true;
    private readonly List<Func<PerformanceMetrics, bool>> _metricsFilters = new();
    private int _optimizationThreads = Environment.ProcessorCount * 2;
    private readonly OptimizerRunner<TStrategy> _optimizer;
    public string HistoryPath { get; set; }
        = Environment.GetEnvironmentVariable("StockSharp__HistoryPath") ?? @".\History";
    public string OutputPath { get; set; }
        = Environment.GetEnvironmentVariable("StockSharp__ResultsPath") ?? @".\Results";

    public new OptimizationLauncher<TStrategy>? WithPortfolio(Portfolio portfolio)
        => base.WithPortfolio(portfolio) as OptimizationLauncher<TStrategy>;

    public new OptimizationLauncher<TStrategy>? WithStrategyParams(params ICustomParam[] parameters)
        => base.WithStrategyParams(parameters) as OptimizationLauncher<TStrategy>;

    public new OptimizationLauncher<TStrategy>? WithParamValidation(Func<IDictionary<string, ICustomParam>, bool> filter)
        => base.WithParamValidation(filter) as OptimizationLauncher<TStrategy>;

    public OptimizationLauncher(PeriodConfig trainingPeriod, OptimizerRunner<TStrategy> customOptimizer)
        : base()
    {
        _optimizer = customOptimizer;
        _trainingPeriod = trainingPeriod ?? throw new ArgumentNullException(nameof(trainingPeriod));
        if (!_trainingPeriod.IsValid())
            throw new ArgumentException("Invalid training period configuration.", nameof(trainingPeriod));

        _commissionRules.AddRange([new CommissionTradeRule { Value = 0.01m }]);
    }

    public OptimizationLauncher<TStrategy> WithCommissionRules(IEnumerable<ICommissionRule> commissionRules)
    {
        if (commissionRules == null || !commissionRules.Any())
            throw new ArgumentException("Commission rules cannot be null or empty.", nameof(commissionRules));

        _commissionRules = commissionRules.ToList();

        return this;
    }

    public OptimizationLauncher<TStrategy> WithOptimizationThreads(int threadCount)
    {
        if (threadCount <= 0)
            throw new ArgumentException("Thread count must be positive.", nameof(threadCount));

        _optimizationThreads = threadCount;
        return this;
    }

    public OptimizationLauncher<TStrategy> WithWalkForward(WalkForwardConfig config)
    {
        _walkForwardConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    protected override void LaunchStrategy(CancellationToken cancellationToken)
    {
        _stockSharpOptimizer = _optimizer.CreateOptimizer(new OptimizationConfig
        {
            HistoryPath = HistoryPath,
            ParamsContainer = ParamsContainer,
            TrainingPeriod = _trainingPeriod,
            ValidationPeriod = _trainingPeriod,//dummy, not used in the current optimization
            InitialCapital = InitialCapital,
            CommissionRules = _commissionRules,
            IsBruteForce = _bruteForce,
            ParallelWorkers = _optimizationThreads,
        });

        ConfigureDiagnostics(_stockSharpOptimizer);
        var optimizationResults = _optimizer.Optimize(); //TODO handle possible cancellation

        var bestSortino = optimizationResults
            .OrderByDescending(r => r.Value.ValidationMetrics?.SortinoRatio ?? 0)
            .FirstOrDefault();

        DisplayDetailedMetricsComparison(bestSortino.Value);

        var resToChart = bestSortino.Value; //VALIDATED STRATEGY IS NULL HERE

        if (resToChart.ValidatedStrategy == null)
            throw new InvalidOperationException("Validated strategy is null");

        var startDate = _trainingPeriod.StartDate.DateTime;
        var endDate = _trainingPeriod.EndDate.DateTime;
        var chartModel = new StrategySecurityChartModel
        {
            StartDate = startDate,
            EndDate = endDate,
            HistoryPath = HistoryPath,
            Security = resToChart.ValidatedStrategy.Securities.Keys.FirstOrDefault()!,
            Strategy = resToChart.ValidatedStrategy,
            OutputPath = Path.Combine(OutputPath, $"{resToChart.ValidatedStrategy.Hash}_{startDate:yyyyMMddTHHmm}_{endDate:yyyyMMddTHHmm}.html"),
            Metrics = resToChart.ValidationMetrics ?? resToChart.TrainingMetrics ?? new PerformanceMetrics(),
            WalkForwardResult = resToChart.WalkForwardResult
        };

        // TODO: choose smarter way to display results
        new ReportBuilder<TStrategy>().GenerateInteractiveChart(chartModel, openInBrowser: true);
    }

    public OptimizationLauncher<TStrategy> WithMetricsFilter(Func<PerformanceMetrics, bool> filter)
    {
        if (filter == null)
            throw new ArgumentNullException(nameof(filter));

        _metricsFilters.Add(filter);
        return this;
    }

    private void DisplayDetailedMetricsComparison(OptimizationResult<TStrategy> result)
    {
        var strategy = result.TrainedStrategy;
        var trainMetrics = result.TrainingMetrics;
        var valMetrics = result.ValidationMetrics;

        if (trainMetrics == null || valMetrics == null)
            return;

        Console.WriteLine($"Strategy ID: {strategy.Id}");
        Console.WriteLine("Parameters:");
        foreach (var param in strategy.Parameters)
        {
            Console.WriteLine($"  {param.Key} = {param.Value}");
        }

        Console.WriteLine("\nDetailed Metrics Comparison:");
        Console.WriteLine($"{"Metric",-20}{"Training",-15}{"Validation",-15}{"Difference",-15}");
        Console.WriteLine(new string('-', 65));

        CompareMetric("Total Return", trainMetrics.TotalReturn, valMetrics.TotalReturn);
        CompareMetric("Annualized Return", trainMetrics.AnnualizedReturn, valMetrics.AnnualizedReturn);
        CompareMetric("Sharpe Ratio", trainMetrics.SharpeRatio, valMetrics.SharpeRatio);
        CompareMetric("Sortino Ratio", trainMetrics.SortinoRatio, valMetrics.SortinoRatio);
        CompareMetric("Max Drawdown", trainMetrics.MaxDrawdown, valMetrics.MaxDrawdown);
        CompareMetric("Win Rate", trainMetrics.WinRate, valMetrics.WinRate);
        CompareMetric("Profit Factor", trainMetrics.ProfitFactor, valMetrics.ProfitFactor);
        CompareMetric("Total Trades", trainMetrics.TotalTrades, valMetrics.TotalTrades);
        CompareMetric("Win/Loss Ratio",
            trainMetrics.WinningTrades / (double)(trainMetrics.LosingTrades == 0 ? 1 : trainMetrics.LosingTrades),
            valMetrics.WinningTrades / (double)(valMetrics.LosingTrades == 0 ? 1 : valMetrics.LosingTrades));
    }

    private void CompareMetric(string metricName, double trainingValue, double validationValue)
    {
        double difference = validationValue - trainingValue;
        string differenceStr = difference >= 0 ? $"+{difference:F2}" : $"{difference:F2}";

        Console.WriteLine($"{metricName,-20}{trainingValue,-15:F2}{validationValue,-15:F2}{differenceStr,-15}");
    }

    public OptimizationLauncher<TStrategy> WithOptimizerConfigure(Action<BaseOptimizer> configDelegate)
    {
        if (_stockSharpOptimizer != null)
            configDelegate(_stockSharpOptimizer);
        return this;
    }

    protected void ConfigureDiagnostics(BaseOptimizer ssOptimizer)
    {
        ssOptimizer.ConnectorInitialized += (connector) =>
        {
            connector.LogLevel = LogLevels.Debug;
            Console.WriteLine($"Subscription started for {connector.Securities.FirstOrDefault()?.Id}");
        };
        ssOptimizer.StrategyInitialized += (strategy, parameters) =>
        {
            Console.WriteLine($"Strategy initialized: {strategy.Security?.Id}, Parameters: {string.Join(", ", parameters.Select(p => $"{p.Id}={p.Value}"))}");
        };
        ssOptimizer.StateChanged += (oldState, newState) =>
        {
            Console.WriteLine($"Optimizer state changed from {oldState} to {newState}");
        };
        ssOptimizer.TotalProgressChanged += (percent, ts1, ts2) =>
        {
            Console.WriteLine($"Total progress: {percent}% ({ts1} - {ts2})");
        };
    }

    protected override void DisposeInherited()
    {
        _stockSharpOptimizer?.Dispose();
        //_localMarketDataDrive.Dispose();
        //_storageRegistry.Dispose();
    }

    protected override bool IsExitRequired()
    {
        return _stockSharpOptimizer != null && _stockSharpOptimizer.State == ChannelStates.Stopped;
    }
}
