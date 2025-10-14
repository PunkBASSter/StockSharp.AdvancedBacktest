using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Pipeline;

public interface IPipelinePhase<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    string PhaseName { get; }

    Task<PipelineContext<TStrategy>> ExecuteAsync(
        PipelineContext<TStrategy> context,
        CancellationToken cancellationToken = default);
}
