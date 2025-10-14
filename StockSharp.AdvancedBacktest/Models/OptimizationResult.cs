using System;
using StockSharp.AdvancedBacktest.Pipeline;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.Models;

public class OptimizationResult<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.Now;
    public required OptimizationConfig Config { get; set; }
    public required TStrategy TrainedStrategy { get; set; }
    public TStrategy? ValidatedStrategy { get; set; }
    public PerformanceMetrics? TrainingMetrics { get; set; }
    public PerformanceMetrics? ValidationMetrics { get; set; }
    public WalkForwardResult? WalkForwardResult { get; set; }

    public string StrategyName => TrainedStrategy?.GetType().Name ?? typeof(TStrategy).Name;
    public string StrategyVersion => TrainedStrategy?.Version ?? "1.0.0";
    public LaunchMode LaunchMode { get; init; } = LaunchMode.Optimization;
    public string ParamsHash { get; init; } = string.Empty;
    public DateTimeOffset TrainingPeriodStart { get; init; }
    public DateTimeOffset TrainingPeriodEnd { get; init; }
    public DateTimeOffset ValidationPeriodStart { get; init; }
    public DateTimeOffset ValidationPeriodEnd { get; init; }
}

