using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Pipeline;

public sealed class PipelineConfiguration
{
    public required string HistoryPath { get; init; }
    public required IReadOnlyList<string> Securities { get; init; }
    public required IReadOnlyList<TimeSpan> TimeFrames { get; init; }
    public required DateTimeOffset TrainingStartDate { get; init; }
    public required DateTimeOffset TrainingEndDate { get; init; }
    public required DateTimeOffset ValidationStartDate { get; init; }
    public required DateTimeOffset ValidationEndDate { get; init; }
    public decimal InitialCapital { get; init; } = 10000m;
    public decimal TradeVolume { get; init; } = 0.01m;
    public decimal CommissionPercentage { get; init; } = 0.1m;
    public bool UseBruteForceOptimization { get; init; } = true;
    public int ParallelWorkers { get; init; } = Environment.ProcessorCount;
    public IReadOnlyDictionary<string, ParameterRangeDefinition>? ParameterRanges { get; init; }
    public IReadOnlyList<Func<IDictionary<string, ICustomParam>, bool>>? ParameterValidationRules { get; init; }
    public IReadOnlyList<Func<PerformanceMetrics, bool>>? MetricFilters { get; init; }
    public int TopStrategiesCount { get; init; } = 5;
    public Func<IEnumerable<PerformanceMetrics>, IEnumerable<PerformanceMetrics>>? SustainabilityFilter { get; init; }
    public string? ExportPath { get; init; }
    public bool GenerateReports { get; init; } = true;
    public bool ExportToJson { get; init; } = true;
}

public sealed class ParameterRangeDefinition
{
    public required string Name { get; init; }
    public required object Min { get; init; }
    public required object Max { get; init; }
    public required object Step { get; init; }
    public required Type ParameterType { get; init; }
}