using StockSharp.BusinessEntities;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Types of risk violations
/// </summary>
public enum RiskViolationType
{
    MaxDrawdownExceeded,
    PositionSizeExceeded,
    DailyLossLimitExceeded,
    OrderValidationFailed,
    EmergencyStop,
    CustomViolation
}

/// <summary>
/// Severity level of risk violations
/// </summary>
public enum RiskSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// Immutable record for risk violation events
/// </summary>
/// <param name="ViolationType">Type of violation</param>
/// <param name="Severity">Severity level</param>
/// <param name="Timestamp">When the violation occurred</param>
/// <param name="Message">Human-readable violation description</param>
/// <param name="CurrentValue">Current value that triggered the violation</param>
/// <param name="Threshold">Threshold that was exceeded</param>
/// <param name="SecurityCode">Security involved (if applicable)</param>
/// <param name="OrderId">Order ID involved (if applicable)</param>
public record RiskViolation(
    [property: JsonPropertyName("violationType")] RiskViolationType ViolationType,
    [property: JsonPropertyName("severity")] RiskSeverity Severity,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("currentValue")] decimal CurrentValue,
    [property: JsonPropertyName("threshold")] decimal Threshold,
    [property: JsonPropertyName("securityCode")] string? SecurityCode = null,
    [property: JsonPropertyName("orderId")] long? OrderId = null
)
{
    /// <summary>
    /// Create a drawdown violation
    /// </summary>
    public static RiskViolation DrawdownExceeded(decimal currentDrawdown, decimal maxAllowed) =>
        new(RiskViolationType.MaxDrawdownExceeded,
            RiskSeverity.Critical,
            DateTimeOffset.UtcNow,
            $"Maximum drawdown exceeded: {currentDrawdown:P2} > {maxAllowed:P2}",
            currentDrawdown,
            maxAllowed);

    /// <summary>
    /// Create a position size violation
    /// </summary>
    public static RiskViolation PositionSizeExceeded(string securityCode, decimal requestedSize, decimal maxAllowed) =>
        new(RiskViolationType.PositionSizeExceeded,
            RiskSeverity.Warning,
            DateTimeOffset.UtcNow,
            $"Position size exceeded for {securityCode}: {requestedSize} > {maxAllowed}",
            requestedSize,
            maxAllowed,
            securityCode);

    /// <summary>
    /// Create a daily loss limit violation
    /// </summary>
    public static RiskViolation DailyLossExceeded(decimal dailyLoss, decimal maxAllowed) =>
        new(RiskViolationType.DailyLossLimitExceeded,
            RiskSeverity.Critical,
            DateTimeOffset.UtcNow,
            $"Daily loss limit exceeded: {dailyLoss:C} > {maxAllowed:C}",
            Math.Abs(dailyLoss),
            Math.Abs(maxAllowed));

    /// <summary>
    /// Create an order validation failure
    /// </summary>
    public static RiskViolation OrderValidationFailed(Order order, string reason) =>
        new(RiskViolationType.OrderValidationFailed,
            RiskSeverity.Warning,
            DateTimeOffset.UtcNow,
            $"Order validation failed: {reason}",
            order.Volume,
            0m,
            order.Security?.Code,
            order.Id);

    /// <summary>
    /// Create an emergency stop violation
    /// </summary>
    public static RiskViolation EmergencyStop(string reason) =>
        new(RiskViolationType.EmergencyStop,
            RiskSeverity.Emergency,
            DateTimeOffset.UtcNow,
            $"Emergency stop triggered: {reason}",
            0m,
            0m);

    /// <summary>
    /// Create a custom violation
    /// </summary>
    public static RiskViolation Custom(string message, decimal currentValue, decimal threshold, RiskSeverity severity = RiskSeverity.Warning) =>
        new(RiskViolationType.CustomViolation,
            severity,
            DateTimeOffset.UtcNow,
            message,
            currentValue,
            threshold);
}