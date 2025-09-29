using System.Collections.Immutable;
using System.Diagnostics;
using StockSharp.AdvancedBacktest.Core.Configuration.Parameters;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

namespace StockSharp.AdvancedBacktest.Core.Optimization;

/// <summary>
/// High-performance optimization runner that evaluates strategies across parameter spaces.
/// Supports streaming evaluation with minimal memory usage and parallel processing.
/// </summary>
public sealed class OptimizationRunner : IDisposable
{
    private readonly ParameterSpaceExplorer _explorer;
    private readonly OptimizationSettings _settings;
    private bool _disposed;

    /// <summary>
    /// Gets the parameter space being explored.
    /// </summary>
    public ParameterSpaceExplorer Explorer => _explorer;

    /// <summary>
    /// Gets the optimization settings.
    /// </summary>
    public OptimizationSettings Settings => _settings;

    /// <summary>
    /// Initializes a new optimization runner.
    /// </summary>
    /// <param name="parameters">Parameter definitions to optimize</param>
    /// <param name="settings">Optimization settings</param>
    public OptimizationRunner(
        ImmutableArray<ParameterDefinitionBase> parameters,
        OptimizationSettings? settings = null)
    {
        _explorer = new ParameterSpaceExplorer(parameters);
        _settings = settings ?? OptimizationSettings.Default;
    }

    /// <summary>
    /// Runs optimization with streaming evaluation and real-time result aggregation.
    /// </summary>
    /// <typeparam name="TStrategy">Strategy type to optimize</typeparam>
    /// <param name="strategyFactory">Factory function to create strategy instances</param>
    /// <param name="evaluator">Function to evaluate strategy performance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optimization results with performance statistics</returns>
    public async Task<OptimizationResults> RunAsync<TStrategy>(
        Func<ImmutableDictionary<string, object?>, TStrategy> strategyFactory,
        Func<TStrategy, CancellationToken, Task<PerformanceMetrics>> evaluator,
        CancellationToken cancellationToken = default)
        where TStrategy : IEnhancedStrategy
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<OptimizationResult>();
        var processed = 0L;
        var skipped = 0L;

        var progress = new Progress<OptimizationProgress>(p =>
        {
            // Report progress if handler is provided
            _settings.ProgressCallback?.Invoke(p);
        });

        try
        {
            if (_settings.UseParallelProcessing && _explorer.TotalCombinations.HasValue)
            {
                // Use batch processing for large parameter spaces
                await ProcessBatchesAsync(strategyFactory, evaluator, results, progress, cancellationToken);
            }
            else
            {
                // Use streaming processing for unknown or small parameter spaces
                await ProcessStreamingAsync(strategyFactory, evaluator, results, progress, cancellationToken);
            }

            stopwatch.Stop();

            var best = results
                .Where(r => r.IsValid)
                .OrderByDescending(r => r.Performance.GetScore(_settings.OptimizationMetric))
                .FirstOrDefault();

            return new OptimizationResults(
                BestResult: best,
                AllResults: results.ToImmutableArray(),
                TotalCombinations: _explorer.TotalCombinations,
                ProcessedCombinations: processed,
                SkippedCombinations: skipped,
                ElapsedTime: stopwatch.Elapsed,
                ThroughputCombinationsPerSecond: processed / stopwatch.Elapsed.TotalSeconds,
                Settings: _settings
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new OptimizationResults(
                BestResult: results.Where(r => r.IsValid)
                    .OrderByDescending(r => r.Performance.GetScore(_settings.OptimizationMetric))
                    .FirstOrDefault(),
                AllResults: results.ToImmutableArray(),
                TotalCombinations: _explorer.TotalCombinations,
                ProcessedCombinations: processed,
                SkippedCombinations: skipped,
                ElapsedTime: stopwatch.Elapsed,
                ThroughputCombinationsPerSecond: processed / stopwatch.Elapsed.TotalSeconds,
                Settings: _settings,
                WasCancelled: true
            );
        }
    }

    private async Task ProcessStreamingAsync<TStrategy>(
        Func<ImmutableDictionary<string, object?>, TStrategy> strategyFactory,
        Func<TStrategy, CancellationToken, Task<PerformanceMetrics>> evaluator,
        List<OptimizationResult> results,
        IProgress<OptimizationProgress> progress,
        CancellationToken cancellationToken)
        where TStrategy : IEnhancedStrategy
    {
        var processed = 0L;
        var lastProgressReport = DateTime.UtcNow;

        await foreach (var parameters in _explorer.EnumerateAsync(cancellationToken))
        {
            var result = await EvaluateParametersAsync(parameters, strategyFactory, evaluator, cancellationToken);
            results.Add(result);
            processed++;

            // Report progress periodically
            if (DateTime.UtcNow - lastProgressReport > TimeSpan.FromSeconds(1))
            {
                progress.Report(new OptimizationProgress(
                    ProcessedCombinations: processed,
                    TotalCombinations: _explorer.TotalCombinations,
                    BestResult: results.Where(r => r.IsValid)
                        .OrderByDescending(r => r.Performance.GetScore(_settings.OptimizationMetric))
                        .FirstOrDefault(),
                    ElapsedTime: DateTime.UtcNow - DateTime.UtcNow.AddSeconds(-1) // Approximate elapsed
                ));
                lastProgressReport = DateTime.UtcNow;
            }

            // Apply early termination if enabled
            if (_settings.EarlyTermination != null && processed % 1000 == 0)
            {
                var shouldTerminate = _settings.EarlyTermination(results.AsReadOnly());
                if (shouldTerminate)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessBatchesAsync<TStrategy>(
        Func<ImmutableDictionary<string, object?>, TStrategy> strategyFactory,
        Func<TStrategy, CancellationToken, Task<PerformanceMetrics>> evaluator,
        List<OptimizationResult> results,
        IProgress<OptimizationProgress> progress,
        CancellationToken cancellationToken)
        where TStrategy : IEnhancedStrategy
    {
        var processed = 0L;

        await foreach (var batch in _explorer.EnumerateBatchesAsync(
            _settings.BatchSize, _settings.MaxDegreeOfParallelism, cancellationToken))
        {
            var batchResults = new OptimizationResult[batch.Length];

            // Process batch in parallel
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(batch.Select((parameters, index) => new { parameters, index }),
                parallelOptions,
                async (item, ct) =>
                {
                    batchResults[item.index] = await EvaluateParametersAsync(
                        item.parameters, strategyFactory, evaluator, ct);
                });

            results.AddRange(batchResults);
            processed += batch.Length;

            // Report progress
            progress.Report(new OptimizationProgress(
                ProcessedCombinations: processed,
                TotalCombinations: _explorer.TotalCombinations,
                BestResult: results.Where(r => r.IsValid)
                    .OrderByDescending(r => r.Performance.GetScore(_settings.OptimizationMetric))
                    .FirstOrDefault(),
                ElapsedTime: TimeSpan.Zero // Will be calculated by caller
            ));
        }
    }

    private async Task<OptimizationResult> EvaluateParametersAsync<TStrategy>(
        ImmutableDictionary<string, object?> parameters,
        Func<ImmutableDictionary<string, object?>, TStrategy> strategyFactory,
        Func<TStrategy, CancellationToken, Task<PerformanceMetrics>> evaluator,
        CancellationToken cancellationToken)
        where TStrategy : IEnhancedStrategy
    {
        try
        {
            using var strategy = strategyFactory(parameters);

            // Validate parameters first
            var validation = strategy.ValidateParameters();
            if (!validation.IsValid)
            {
                return new OptimizationResult(
                    Parameters: parameters,
                    Performance: PerformanceMetrics.Empty,
                    IsValid: false,
                    ErrorMessage: validation.GetFormattedIssues(),
                    EvaluationTime: TimeSpan.Zero
                );
            }

            var evaluationStart = Stopwatch.StartNew();
            var performance = await evaluator(strategy, cancellationToken);
            evaluationStart.Stop();

            return new OptimizationResult(
                Parameters: parameters,
                Performance: performance,
                IsValid: true,
                ErrorMessage: null,
                EvaluationTime: evaluationStart.Elapsed
            );
        }
        catch (Exception ex)
        {
            return new OptimizationResult(
                Parameters: parameters,
                Performance: PerformanceMetrics.Empty,
                IsValid: false,
                ErrorMessage: ex.Message,
                EvaluationTime: TimeSpan.Zero
            );
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _explorer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Optimization settings and configuration.
/// </summary>
/// <param name="OptimizationMetric">Primary metric to optimize for</param>
/// <param name="UseParallelProcessing">Whether to use parallel batch processing</param>
/// <param name="BatchSize">Size of batches for parallel processing</param>
/// <param name="MaxDegreeOfParallelism">Maximum number of parallel threads</param>
/// <param name="EarlyTermination">Optional early termination condition</param>
/// <param name="ProgressCallback">Optional progress reporting callback</param>
public record OptimizationSettings(
    OptimizationMetric OptimizationMetric = OptimizationMetric.SharpeRatio,
    bool UseParallelProcessing = true,
    int BatchSize = 1000,
    int MaxDegreeOfParallelism = -1,
    Func<IReadOnlyList<OptimizationResult>, bool>? EarlyTermination = null,
    Action<OptimizationProgress>? ProgressCallback = null
)
{
    /// <summary>
    /// Default optimization settings.
    /// </summary>
    public static OptimizationSettings Default { get; } = new();
}

/// <summary>
/// Metrics available for optimization.
/// </summary>
public enum OptimizationMetric
{
    SharpeRatio,
    TotalReturn,
    MaxDrawdown,
    WinRate,
    ProfitFactor,
    CalmarRatio
}

/// <summary>
/// Progress information during optimization.
/// </summary>
/// <param name="ProcessedCombinations">Number of combinations processed</param>
/// <param name="TotalCombinations">Total combinations to process (null if unknown)</param>
/// <param name="BestResult">Current best result</param>
/// <param name="ElapsedTime">Time elapsed since optimization start</param>
public record OptimizationProgress(
    long ProcessedCombinations,
    long? TotalCombinations,
    OptimizationResult? BestResult,
    TimeSpan ElapsedTime
)
{
    /// <summary>
    /// Gets the completion percentage (0-100) or null if total is unknown.
    /// </summary>
    public double? CompletionPercentage =>
        TotalCombinations.HasValue
            ? Math.Min(100.0, ProcessedCombinations * 100.0 / TotalCombinations.Value)
            : null;
}

/// <summary>
/// Results from a single parameter combination evaluation.
/// </summary>
/// <param name="Parameters">Parameter values used</param>
/// <param name="Performance">Performance metrics achieved</param>
/// <param name="IsValid">Whether the evaluation was successful</param>
/// <param name="ErrorMessage">Error message if evaluation failed</param>
/// <param name="EvaluationTime">Time taken to evaluate this combination</param>
public record OptimizationResult(
    ImmutableDictionary<string, object?> Parameters,
    PerformanceMetrics Performance,
    bool IsValid,
    string? ErrorMessage,
    TimeSpan EvaluationTime
);

/// <summary>
/// Complete optimization results.
/// </summary>
/// <param name="BestResult">Best performing parameter combination</param>
/// <param name="AllResults">All evaluated combinations</param>
/// <param name="TotalCombinations">Total combinations in parameter space</param>
/// <param name="ProcessedCombinations">Number of combinations actually processed</param>
/// <param name="SkippedCombinations">Number of combinations skipped</param>
/// <param name="ElapsedTime">Total optimization time</param>
/// <param name="ThroughputCombinationsPerSecond">Processing throughput</param>
/// <param name="Settings">Optimization settings used</param>
/// <param name="WasCancelled">Whether optimization was cancelled</param>
public record OptimizationResults(
    OptimizationResult? BestResult,
    ImmutableArray<OptimizationResult> AllResults,
    long? TotalCombinations,
    long ProcessedCombinations,
    long SkippedCombinations,
    TimeSpan ElapsedTime,
    double ThroughputCombinationsPerSecond,
    OptimizationSettings Settings,
    bool WasCancelled = false
);

/// <summary>
/// Performance metrics for strategy evaluation.
/// </summary>
/// <param name="TotalReturn">Total return percentage</param>
/// <param name="SharpeRatio">Sharpe ratio</param>
/// <param name="MaxDrawdown">Maximum drawdown percentage</param>
/// <param name="WinRate">Win rate percentage</param>
/// <param name="ProfitFactor">Profit factor</param>
/// <param name="CalmarRatio">Calmar ratio</param>
public record PerformanceMetrics(
    double TotalReturn,
    double SharpeRatio,
    double MaxDrawdown,
    double WinRate,
    double ProfitFactor,
    double CalmarRatio
)
{
    /// <summary>
    /// Empty performance metrics for invalid results.
    /// </summary>
    public static PerformanceMetrics Empty { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Gets the score for a specific optimization metric.
    /// </summary>
    /// <param name="metric">Metric to get score for</param>
    /// <returns>Score value (higher is better)</returns>
    public double GetScore(OptimizationMetric metric) => metric switch
    {
        OptimizationMetric.SharpeRatio => SharpeRatio,
        OptimizationMetric.TotalReturn => TotalReturn,
        OptimizationMetric.MaxDrawdown => -MaxDrawdown, // Negative because lower drawdown is better
        OptimizationMetric.WinRate => WinRate,
        OptimizationMetric.ProfitFactor => ProfitFactor,
        OptimizationMetric.CalmarRatio => CalmarRatio,
        _ => throw new ArgumentException($"Unknown metric: {metric}")
    };
}