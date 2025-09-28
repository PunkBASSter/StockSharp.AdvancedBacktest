using Microsoft.Extensions.Logging;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Concurrent;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Risk management implementation with configurable limits and violation tracking
/// </summary>
public class RiskManager : IRiskManager
{
    private readonly ILogger<RiskManager> _logger;
    private readonly CircularBuffer<RiskViolation> _violationHistory;
    private readonly ConcurrentDictionary<string, decimal> _positionSizes = new();
    private readonly ConcurrentDictionary<DateOnly, decimal> _dailyPnL = new();

    private decimal _maxDrawdownLimit = 0.10m; // 10%
    private decimal _maxPositionSize = 1_000_000m;
    private decimal _dailyLossLimit = 50_000m;
    private volatile bool _isDisposed;

    private decimal _currentRiskLevel;
    private readonly object _riskCalculationLock = new();

    /// <summary>
    /// Maximum allowed drawdown percentage
    /// </summary>
    public decimal MaxDrawdownLimit
    {
        get => _maxDrawdownLimit;
        set => _maxDrawdownLimit = Math.Max(0m, Math.Min(1m, value)); // Clamp between 0-100%
    }

    /// <summary>
    /// Maximum position size
    /// </summary>
    public decimal MaxPositionSize
    {
        get => _maxPositionSize;
        set => _maxPositionSize = Math.Max(0m, value);
    }

    /// <summary>
    /// Daily loss limit
    /// </summary>
    public decimal DailyLossLimit
    {
        get => _dailyLossLimit;
        set => _dailyLossLimit = Math.Max(0m, value);
    }

    /// <summary>
    /// Current risk level (0-1 scale)
    /// </summary>
    public decimal CurrentRiskLevel => _currentRiskLevel;

    /// <summary>
    /// Whether risk limits are currently breached
    /// </summary>
    public bool IsRiskLimitBreached => _currentRiskLevel >= 0.8m; // 80% threshold

    /// <summary>
    /// Initialize risk manager with configurable violation history size
    /// </summary>
    public RiskManager(ILogger<RiskManager> logger, int violationHistorySize = 100)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _violationHistory = new CircularBuffer<RiskViolation>(violationHistorySize);

        _logger.LogDebug("Risk manager initialized with violation history size {HistorySize}", violationHistorySize);
    }

    /// <summary>
    /// Validate an order before execution
    /// </summary>
    public bool ValidateOrder(Order order)
    {
        if (_isDisposed || order == null)
            return false;

        try
        {
            var violations = new List<RiskViolation>();

            // Validate position size
            if (order.Security != null)
            {
                var newPositionSize = GetProjectedPositionSize(order.Security.Code, order.Volume, order.Direction);
                if (Math.Abs(newPositionSize) > _maxPositionSize)
                {
                    var violation = RiskViolation.PositionSizeExceeded(
                        order.Security.Code,
                        Math.Abs(newPositionSize),
                        _maxPositionSize);
                    violations.Add(violation);
                }
            }

            // Validate order volume
            if (Math.Abs(order.Volume) > _maxPositionSize)
            {
                var violation = RiskViolation.OrderValidationFailed(order,
                    $"Order volume {order.Volume} exceeds maximum position size {_maxPositionSize}");
                violations.Add(violation);
            }

            // Record violations
            foreach (var violation in violations)
            {
                RecordViolation(violation);
            }

            var isValid = violations.Count == 0;

            if (!isValid)
            {
                _logger.LogWarning("Order {OrderId} failed risk validation: {ViolationCount} violations",
                    order.Id, violations.Count);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating order {OrderId}", order.Id);
            return false; // Fail safe
        }
    }

    /// <summary>
    /// Validate a position size
    /// </summary>
    public bool ValidatePositionSize(Security security, decimal volume)
    {
        if (_isDisposed || security == null)
            return false;

        try
        {
            var currentPosition = _positionSizes.GetValueOrDefault(security.Code, 0m);
            var newPosition = currentPosition + volume;

            var isValid = Math.Abs(newPosition) <= _maxPositionSize;

            if (!isValid)
            {
                var violation = RiskViolation.PositionSizeExceeded(
                    security.Code,
                    Math.Abs(newPosition),
                    _maxPositionSize);
                RecordViolation(violation);

                _logger.LogWarning("Position size validation failed for {SecurityCode}: {NewPosition} > {MaxSize}",
                    security.Code, Math.Abs(newPosition), _maxPositionSize);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating position size for {SecurityCode}", security.Code);
            return false; // Fail safe
        }
    }

    /// <summary>
    /// Check if drawdown limit is breached
    /// </summary>
    public bool IsDrawdownLimitBreached(decimal currentDrawdown)
    {
        var isBreached = currentDrawdown > _maxDrawdownLimit;

        if (isBreached)
        {
            var violation = RiskViolation.DrawdownExceeded(currentDrawdown, _maxDrawdownLimit);
            RecordViolation(violation);

            _logger.LogCritical("Drawdown limit breached: {CurrentDrawdown:P2} > {MaxDrawdown:P2}",
                currentDrawdown, _maxDrawdownLimit);
        }

        return isBreached;
    }

    /// <summary>
    /// Check if daily loss limit is breached
    /// </summary>
    public bool IsDailyLossLimitBreached(decimal dailyPnL)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentDailyLoss = Math.Abs(Math.Min(0m, dailyPnL));
        var isBreached = currentDailyLoss > _dailyLossLimit;

        // Update daily PnL tracking
        _dailyPnL.AddOrUpdate(today, dailyPnL, (_, existing) => existing + dailyPnL);

        if (isBreached)
        {
            var violation = RiskViolation.DailyLossExceeded(dailyPnL, _dailyLossLimit);
            RecordViolation(violation);

            _logger.LogCritical("Daily loss limit breached: {DailyLoss:C} > {DailyLimit:C}",
                currentDailyLoss, _dailyLossLimit);
        }

        return isBreached;
    }

    /// <summary>
    /// Record a risk violation
    /// </summary>
    public void RecordViolation(RiskViolation violation)
    {
        if (_isDisposed || violation == null)
            return;

        try
        {
            lock (_riskCalculationLock)
            {
                _violationHistory.Add(violation);
                UpdateRiskLevel();
            }

            _logger.LogWarning("Risk violation recorded: {ViolationType} - {Message} (Severity: {Severity})",
                violation.ViolationType, violation.Message, violation.Severity);

            // Trigger emergency stop for critical violations
            if (violation.Severity == RiskSeverity.Emergency)
            {
                _ = Task.Run(async () => await EmergencyStopAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording risk violation: {ViolationType}", violation.ViolationType);
        }
    }

    /// <summary>
    /// Get recent risk violations
    /// </summary>
    public IReadOnlyList<RiskViolation> GetRecentViolations(int count = 10)
    {
        if (_isDisposed)
            return Array.Empty<RiskViolation>();

        lock (_riskCalculationLock)
        {
            var totalCount = Math.Min(count, _violationHistory.Count);
            var violations = new RiskViolation[totalCount];

            for (int i = 0; i < totalCount; i++)
            {
                violations[i] = _violationHistory[_violationHistory.Count - totalCount + i];
            }

            return violations;
        }
    }

    /// <summary>
    /// Reset daily risk counters
    /// </summary>
    public void ResetDaily()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Clean up old daily PnL data (keep last 30 days)
            var cutoffDate = today.AddDays(-30);
            var keysToRemove = _dailyPnL.Keys.Where(date => date < cutoffDate).ToList();

            foreach (var key in keysToRemove)
            {
                _dailyPnL.TryRemove(key, out _);
            }

            // Reset current day PnL
            _dailyPnL.TryRemove(today, out _);

            lock (_riskCalculationLock)
            {
                UpdateRiskLevel();
            }

            _logger.LogDebug("Daily risk counters reset for {Date}", today);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting daily risk counters");
        }
    }

    /// <summary>
    /// Emergency stop all positions
    /// </summary>
    public async Task EmergencyStopAsync()
    {
        if (_isDisposed)
            return;

        try
        {
            _logger.LogCritical("EMERGENCY STOP TRIGGERED - All trading activity must be halted");

            var violation = RiskViolation.EmergencyStop("Risk limits exceeded - emergency stop activated");
            RecordViolation(violation);

            // Clear all position tracking
            _positionSizes.Clear();

            // In a real implementation, this would:
            // 1. Cancel all pending orders
            // 2. Close all positions
            // 3. Disable further trading
            // 4. Send notifications to administrators

            await Task.Delay(100); // Simulate async operation

            _logger.LogCritical("Emergency stop procedures completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop procedure");
        }
    }

    /// <summary>
    /// Update position size tracking
    /// </summary>
    public void UpdatePositionSize(string securityCode, decimal positionSize)
    {
        if (_isDisposed || string.IsNullOrEmpty(securityCode))
            return;

        _positionSizes.AddOrUpdate(securityCode, positionSize, (_, _) => positionSize);

        lock (_riskCalculationLock)
        {
            UpdateRiskLevel();
        }
    }

    /// <summary>
    /// Get projected position size after order execution
    /// </summary>
    private decimal GetProjectedPositionSize(string securityCode, decimal volume, Sides direction)
    {
        var currentPosition = _positionSizes.GetValueOrDefault(securityCode, 0m);
        var volumeWithDirection = direction == Sides.Buy ? volume : -volume;
        return currentPosition + volumeWithDirection;
    }

    /// <summary>
    /// Update current risk level based on recent violations and metrics
    /// </summary>
    private void UpdateRiskLevel()
    {
        try
        {
            decimal riskScore = 0m;

            // Recent violations contribute to risk score
            var recentViolations = GetRecentViolations(10);
            var criticalViolations = recentViolations.Count(v => v.Severity >= RiskSeverity.Critical);
            var warningViolations = recentViolations.Count(v => v.Severity == RiskSeverity.Warning);

            riskScore += criticalViolations * 0.3m; // Critical violations have high impact
            riskScore += warningViolations * 0.1m;  // Warning violations have moderate impact

            // Position concentration risk
            var totalPositions = _positionSizes.Values.Sum(Math.Abs);
            if (totalPositions > 0)
            {
                var largestPosition = _positionSizes.Values.Max(Math.Abs);
                var concentrationRatio = largestPosition / totalPositions;
                riskScore += concentrationRatio * 0.2m; // High concentration increases risk
            }

            // Daily loss risk
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (_dailyPnL.TryGetValue(today, out var todayPnL) && todayPnL < 0)
            {
                var lossRatio = Math.Abs(todayPnL) / _dailyLossLimit;
                riskScore += lossRatio * 0.4m; // Daily losses significantly impact risk
            }

            // Clamp risk level between 0 and 1
            _currentRiskLevel = Math.Max(0m, Math.Min(1m, riskScore));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating risk level");
            _currentRiskLevel = 1m; // Fail safe - assume maximum risk
        }
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _violationHistory.Clear();
            _positionSizes.Clear();
            _dailyPnL.Clear();

            _isDisposed = true;
            _logger.LogDebug("Risk manager disposed");
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}