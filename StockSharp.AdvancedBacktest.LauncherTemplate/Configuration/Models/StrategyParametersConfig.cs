using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

public class StrategyParametersConfig
{
    [Required(ErrorMessage = "Strategy name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Strategy name must be between 1 and 100 characters")]
    public required string StrategyName { get; set; }

    [Required(ErrorMessage = "Strategy version is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Strategy version must be between 1 and 50 characters")]
    public required string StrategyVersion { get; set; }

    [Required(ErrorMessage = "Strategy hash is required")]
    [StringLength(64, MinimumLength = 32, ErrorMessage = "Strategy hash must be between 32 and 64 characters")]
    public required string StrategyHash { get; set; }

    [Required(ErrorMessage = "Optimization date is required")]
    public required DateTimeOffset OptimizationDate { get; set; }

    [Required(ErrorMessage = "Parameters are required")]
    public required Dictionary<string, JsonElement> Parameters { get; set; }

    public PerformanceMetrics? TrainingMetrics { get; set; }

    public PerformanceMetrics? ValidationMetrics { get; set; }

    public PerformanceMetrics? WalkForwardMetrics { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Initial capital must be greater than 0")]
    public decimal InitialCapital { get; set; } = 10000m;

    [Range(0.001, double.MaxValue, ErrorMessage = "Trade volume must be greater than 0")]
    public decimal TradeVolume { get; set; } = 0.01m;

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }

    public List<string> Securities { get; set; } = [];

    public DateTimeOffset? LastUsedDate { get; set; }

    public bool IsActive { get; set; } = true;
}
