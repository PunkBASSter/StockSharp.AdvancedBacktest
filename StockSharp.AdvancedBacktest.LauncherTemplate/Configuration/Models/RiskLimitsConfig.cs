using System.ComponentModel.DataAnnotations;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

public class RiskLimitsConfig
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Max position size must be greater than 0")]
    public decimal MaxPositionSize { get; set; } = 1000m;

    [Range(0.01, double.MaxValue, ErrorMessage = "Max daily loss must be greater than 0")]
    public decimal MaxDailyLoss { get; set; } = 500m;

    public bool MaxDailyLossIsPercentage { get; set; } = false;

    [Range(0.01, 100, ErrorMessage = "Max drawdown percentage must be between 0.01 and 100")]
    public decimal MaxDrawdownPercentage { get; set; } = 10m;

    [Range(1, int.MaxValue, ErrorMessage = "Max trades per day must be at least 1")]
    public int MaxTradesPerDay { get; set; } = 100;

    public bool CircuitBreakerEnabled { get; set; } = true;

    [Range(0.01, 100, ErrorMessage = "Circuit breaker threshold must be between 0.01 and 100")]
    public decimal CircuitBreakerThresholdPercentage { get; set; } = 5m;

    public int CircuitBreakerCooldownMinutes { get; set; } = 30;

    public decimal MaxLeverageRatio { get; set; } = 1.0m;

    [Range(0, 100, ErrorMessage = "Max position concentration must be between 0 and 100")]
    public decimal MaxPositionConcentrationPercentage { get; set; } = 20m;
}
