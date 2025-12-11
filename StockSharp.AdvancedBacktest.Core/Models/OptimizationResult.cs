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
}
