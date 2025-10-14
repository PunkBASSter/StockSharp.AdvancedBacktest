using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Pipeline;

public sealed class PipelineResult<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset CompletionTime { get; init; }
    public TimeSpan Duration => CompletionTime - StartTime;
    public required bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public required PipelineContext<TStrategy> FinalContext { get; init; }
    public OptimizationResult<TStrategy>? BestStrategy =>
        FinalContext.ValidatedResults?.FirstOrDefault();
    public IReadOnlyList<OptimizationResult<TStrategy>>? ValidatedResults =>
        FinalContext.ValidatedResults;
    public IReadOnlyList<string>? ExportedArtifacts =>
        FinalContext.ExportedArtifacts;
}
