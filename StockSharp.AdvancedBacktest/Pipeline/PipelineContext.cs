using System.Collections.ObjectModel;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Pipeline;

public sealed class PipelineContext<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    public required string StrategyName { get; init; }
    public required string StrategyVersion { get; init; }
    public required string PipelineId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required LaunchMode LaunchMode { get; init; }
    public required PipelineConfiguration Configuration { get; init; }

    /// <summary>
    /// Gets the parameter container populated by Phase 1.
    /// </summary>
    public CustomParamsContainer? ParameterContainer { get; init; }

    /// <summary>
    /// Gets the optimization results populated by Phase 2, keyed by strategy hash.
    /// </summary>
    public IReadOnlyDictionary<string, OptimizationResult<TStrategy>>? OptimizationResults { get; init; }

    /// <summary>
    /// Gets the filtered results populated by Phase 3.
    /// </summary>
    public IReadOnlyList<OptimizationResult<TStrategy>>? FilteredResults { get; init; }

    /// <summary>
    /// Gets the validated results populated by Phase 4.
    /// </summary>
    public IReadOnlyList<OptimizationResult<TStrategy>>? ValidatedResults { get; init; }

    /// <summary>
    /// Gets the exported artifact paths populated by Phase 5.
    /// </summary>
    public IReadOnlyList<string>? ExportedArtifacts { get; init; }

    /// <summary>
    /// Gets the diagnostics dictionary for tracking phase execution metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Diagnostics { get; init; } =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

    /// <summary>
    /// Creates a new context with updated properties while preserving unchanged ones.
    /// </summary>
    public PipelineContext<TStrategy> With(
        string? strategyName = null,
        string? strategyVersion = null,
        string? pipelineId = null,
        DateTimeOffset? createdAt = null,
        LaunchMode? launchMode = null,
        PipelineConfiguration? configuration = null,
        CustomParamsContainer? parameterContainer = null,
        IReadOnlyDictionary<string, OptimizationResult<TStrategy>>? optimizationResults = null,
        IReadOnlyList<OptimizationResult<TStrategy>>? filteredResults = null,
        IReadOnlyList<OptimizationResult<TStrategy>>? validatedResults = null,
        IReadOnlyList<string>? exportedArtifacts = null,
        IReadOnlyDictionary<string, object>? diagnostics = null)
    {
        return new PipelineContext<TStrategy>
        {
            StrategyName = strategyName ?? StrategyName,
            StrategyVersion = strategyVersion ?? StrategyVersion,
            PipelineId = pipelineId ?? PipelineId,
            CreatedAt = createdAt ?? CreatedAt,
            LaunchMode = launchMode ?? LaunchMode,
            Configuration = configuration ?? Configuration,
            ParameterContainer = parameterContainer ?? ParameterContainer,
            OptimizationResults = optimizationResults ?? OptimizationResults,
            FilteredResults = filteredResults ?? FilteredResults,
            ValidatedResults = validatedResults ?? ValidatedResults,
            ExportedArtifacts = exportedArtifacts ?? ExportedArtifacts,
            Diagnostics = diagnostics ?? Diagnostics
        };
    }

    /// <summary>
    /// Creates a new context with updated diagnostics, merging with existing values.
    /// </summary>
    public PipelineContext<TStrategy> WithDiagnostics(IDictionary<string, object> additionalDiagnostics)
    {
        var merged = new Dictionary<string, object>(Diagnostics);
        foreach (var kvp in additionalDiagnostics)
        {
            merged[kvp.Key] = kvp.Value;
        }
        return With(diagnostics: new ReadOnlyDictionary<string, object>(merged));
    }
}
