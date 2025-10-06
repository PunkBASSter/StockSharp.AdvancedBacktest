using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration.Models;

public class LiveTradingConfiguration
{
    [Required(ErrorMessage = "Strategy configuration path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Strategy config path must be between 1 and 500 characters")]
    public required string StrategyConfigPath { get; set; }

    [Required(ErrorMessage = "Broker configuration path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Broker config path must be between 1 and 500 characters")]
    public required string BrokerConfigPath { get; set; }

    [Required(ErrorMessage = "Risk limits are required")]
    public required RiskLimitsConfig RiskLimits { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Safety check interval must be at least 1 second")]
    public int SafetyCheckIntervalSeconds { get; set; } = 10;

    [Range(1, int.MaxValue, ErrorMessage = "Position sync interval must be at least 1 second")]
    public int PositionSyncIntervalSeconds { get; set; } = 30;

    [Range(1, int.MaxValue, ErrorMessage = "Health check interval must be at least 1 second")]
    public int HealthCheckIntervalSeconds { get; set; } = 60;

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    public bool EnableConsoleLogging { get; set; } = true;

    public bool EnableFileLogging { get; set; } = true;

    [StringLength(500, ErrorMessage = "Log file path cannot exceed 500 characters")]
    public string? LogFilePath { get; set; }

    public bool EnableAlerts { get; set; } = true;

    [EmailAddress(ErrorMessage = "Invalid email address format")]
    [StringLength(100, ErrorMessage = "Alert email cannot exceed 100 characters")]
    public string? AlertEmail { get; set; }

    [StringLength(500, ErrorMessage = "Webhook URL cannot exceed 500 characters")]
    [Url(ErrorMessage = "Invalid webhook URL format")]
    public string? AlertWebhookUrl { get; set; }

    public bool EnableAutoRecovery { get; set; } = true;

    [Range(1, 10, ErrorMessage = "Max recovery attempts must be between 1 and 10")]
    public int MaxRecoveryAttempts { get; set; } = 3;

    [Range(1, 300, ErrorMessage = "Recovery delay must be between 1 and 300 seconds")]
    public int RecoveryDelaySeconds { get; set; } = 30;

    public bool EnableDryRun { get; set; } = false;

    public bool RequireManualApproval { get; set; } = false;

    [Range(0, 24, ErrorMessage = "Session timeout must be between 0 and 24 hours")]
    public int SessionTimeoutHours { get; set; } = 12;

    public List<TradingSession> TradingSessions { get; set; } = [];

    [StringLength(500, ErrorMessage = "Database connection string cannot exceed 500 characters")]
    public string? DatabaseConnectionString { get; set; }

    public bool EnablePerformanceMonitoring { get; set; } = true;

    public bool EnableTradeJournal { get; set; } = true;
}

public class TradingSession
{
    [Required(ErrorMessage = "Session name is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Session name must be between 1 and 50 characters")]
    public required string Name { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    public required TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "End time is required")]
    public required TimeOnly EndTime { get; set; }

    [Required(ErrorMessage = "At least one day of week must be specified")]
    public required List<DayOfWeek> DaysOfWeek { get; set; }

    public bool Enabled { get; set; } = true;

    [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
    public string? Description { get; set; }
}
