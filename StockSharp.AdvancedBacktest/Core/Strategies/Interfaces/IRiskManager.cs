using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

/// <summary>
/// Interface for strategy risk management
/// </summary>
public interface IRiskManager : IDisposable
{
    /// <summary>
    /// Maximum allowed drawdown percentage
    /// </summary>
    decimal MaxDrawdownLimit { get; set; }

    /// <summary>
    /// Maximum position size
    /// </summary>
    decimal MaxPositionSize { get; set; }

    /// <summary>
    /// Daily loss limit
    /// </summary>
    decimal DailyLossLimit { get; set; }

    /// <summary>
    /// Current risk level (0-1 scale)
    /// </summary>
    decimal CurrentRiskLevel { get; }

    /// <summary>
    /// Whether risk limits are currently breached
    /// </summary>
    bool IsRiskLimitBreached { get; }

    /// <summary>
    /// Validate an order before execution
    /// </summary>
    /// <param name="order">Order to validate</param>
    /// <returns>True if order passes risk checks</returns>
    bool ValidateOrder(Order order);

    /// <summary>
    /// Validate a position size
    /// </summary>
    /// <param name="security">Security for the position</param>
    /// <param name="volume">Proposed volume</param>
    /// <returns>True if position size is within limits</returns>
    bool ValidatePositionSize(Security security, decimal volume);

    /// <summary>
    /// Check if drawdown limit is breached
    /// </summary>
    /// <param name="currentDrawdown">Current drawdown percentage</param>
    /// <returns>True if limit is breached</returns>
    bool IsDrawdownLimitBreached(decimal currentDrawdown);

    /// <summary>
    /// Check if daily loss limit is breached
    /// </summary>
    /// <param name="dailyPnL">Daily profit/loss</param>
    /// <returns>True if limit is breached</returns>
    bool IsDailyLossLimitBreached(decimal dailyPnL);

    /// <summary>
    /// Record a risk violation
    /// </summary>
    /// <param name="violation">Risk violation details</param>
    void RecordViolation(RiskViolation violation);

    /// <summary>
    /// Get recent risk violations
    /// </summary>
    /// <param name="count">Number of violations to retrieve</param>
    /// <returns>Recent violations</returns>
    IReadOnlyList<RiskViolation> GetRecentViolations(int count = 10);

    /// <summary>
    /// Reset daily risk counters
    /// </summary>
    void ResetDaily();

    /// <summary>
    /// Emergency stop all positions
    /// </summary>
    /// <returns>Task representing the emergency stop operation</returns>
    Task EmergencyStopAsync();
}