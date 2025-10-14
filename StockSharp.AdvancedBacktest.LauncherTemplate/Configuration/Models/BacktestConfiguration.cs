using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

public enum RunMode
{
    /// <summary>
    /// Run parameter optimization across ranges (default behavior)
    /// </summary>
    Optimization,

    /// <summary>
    /// Execute a single backtest with fixed parameter values (no optimization)
    /// </summary>
    Single
}

public class BacktestConfiguration
{
    [Required(ErrorMessage = "Strategy name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Strategy name must be between 1 and 100 characters")]
    public required string StrategyName { get; set; }

    [Required(ErrorMessage = "Strategy version is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Strategy version must be between 1 and 50 characters")]
    public required string StrategyVersion { get; set; }

    [StringLength(1000, ErrorMessage = "Strategy description cannot exceed 1000 characters")]
    public string? StrategyDescription { get; set; }

    [Required(ErrorMessage = "Training start date is required")]
    public required DateTimeOffset TrainingStartDate { get; set; }

    [Required(ErrorMessage = "Training end date is required")]
    public required DateTimeOffset TrainingEndDate { get; set; }

    [Required(ErrorMessage = "Validation start date is required")]
    public required DateTimeOffset ValidationStartDate { get; set; }

    [Required(ErrorMessage = "Validation end date is required")]
    public required DateTimeOffset ValidationEndDate { get; set; }

    [Required(ErrorMessage = "At least one security must be specified")]
    public required List<string> Securities { get; set; }

    [Required(ErrorMessage = "At least one timeframe must be specified")]
    public List<TimeSpan> TimeFrames { get; set; } = [TimeSpan.FromDays(1)];

    /// <summary>
    /// Execution mode: Optimization (default) or Single run
    /// </summary>
    public RunMode RunMode { get; set; } = RunMode.Optimization;

    /// <summary>
    /// Optimizable parameters (required when RunMode = Optimization)
    /// </summary>
    public Dictionary<string, ParameterDefinition>? OptimizableParameters { get; set; }

    public Dictionary<string, JsonElement> FixedParameters { get; set; } = [];

    public List<string> MetricFilterExpressions { get; set; } = [];

    public WalkForwardConfig? WalkForwardConfig { get; set; }

    [Required(ErrorMessage = "History path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "History path must be between 1 and 500 characters")]
    public required string HistoryPath { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Initial capital must be greater than 0")]
    public decimal InitialCapital { get; set; } = 10000m;

    [Range(0.001, double.MaxValue, ErrorMessage = "Trade volume must be greater than 0")]
    public decimal TradeVolume { get; set; } = 0.01m;

    [Range(0, 100, ErrorMessage = "Commission percentage must be between 0 and 100")]
    public decimal CommissionPercentage { get; set; } = 0.1m;

    [Range(1, int.MaxValue, ErrorMessage = "Parallel workers must be at least 1")]
    public int ParallelWorkers { get; set; } = Environment.ProcessorCount;

    public bool UseBruteForceOptimization { get; set; } = true;

    [StringLength(500, ErrorMessage = "Export path cannot exceed 500 characters")]
    public string? ExportPath { get; set; }

    public bool ExportDetailedMetrics { get; set; } = true;

    public bool ExportTradeLog { get; set; } = false;
}

public class ParameterDefinition
{
    [Required(ErrorMessage = "Parameter name is required")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Parameter type is required")]
    public required string Type { get; set; }

    public JsonElement? MinValue { get; set; }

    public JsonElement? MaxValue { get; set; }

    public JsonElement? StepValue { get; set; }

    public List<string>? Values { get; set; }

    public JsonElement? DefaultValue { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
}