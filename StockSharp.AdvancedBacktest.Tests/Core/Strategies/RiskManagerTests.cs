/*using Microsoft.Extensions.Logging.Abstractions;
using StockSharp.AdvancedBacktest.Core.Strategies;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests.Core.Strategies;

/// <summary>
/// Unit tests for RiskManager implementation
/// </summary>
public class RiskManagerTests
{
    private readonly RiskManager _riskManager;

    public RiskManagerTests()
    {
        _riskManager = new RiskManager(NullLogger<RiskManager>.Instance);
    }

    [Fact]
    public void RiskManager_InitialState_ShouldHaveDefaultLimits()
    {
        // Assert
        Assert.Equal(0.10m, _riskManager.MaxDrawdownLimit); // 10%
        Assert.Equal(1_000_000m, _riskManager.MaxPositionSize);
        Assert.Equal(50_000m, _riskManager.DailyLossLimit);
        Assert.Equal(0m, _riskManager.CurrentRiskLevel);
        Assert.False(_riskManager.IsRiskLimitBreached);
    }

    [Fact]
    public void RiskManager_SetLimits_ShouldUpdateCorrectly()
    {
        // Act
        _riskManager.MaxDrawdownLimit = 0.15m; // 15%
        _riskManager.MaxPositionSize = 500_000m;
        _riskManager.DailyLossLimit = 25_000m;

        // Assert
        Assert.Equal(0.15m, _riskManager.MaxDrawdownLimit);
        Assert.Equal(500_000m, _riskManager.MaxPositionSize);
        Assert.Equal(25_000m, _riskManager.DailyLossLimit);
    }

    [Fact]
    public void RiskManager_SetLimits_ShouldClampToValidRanges()
    {
        // Act
        _riskManager.MaxDrawdownLimit = -0.1m; // Negative
        _riskManager.MaxPositionSize = -1000m; // Negative

        // Assert
        Assert.Equal(0m, _riskManager.MaxDrawdownLimit); // Should be clamped to 0
        Assert.Equal(0m, _riskManager.MaxPositionSize); // Should be clamped to 0
    }

    [Fact]
    public void RiskManager_ValidateOrder_WithValidOrder_ShouldReturnTrue()
    {
        // Arrange
        var security = new Security { Code = "AAPL" };
        var order = new Order
        {
            Id = 1,
            Security = security,
            Volume = 100,
            Price = 150m,
            Direction = OrderDirection.Buy
        };

        // Act
        var isValid = _riskManager.ValidateOrder(order);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void RiskManager_ValidateOrder_WithExcessiveVolume_ShouldReturnFalse()
    {
        // Arrange
        var security = new Security { Code = "AAPL" };
        var order = new Order
        {
            Id = 1,
            Security = security,
            Volume = 2_000_000, // Exceeds default max position size
            Price = 150m,
            Direction = OrderDirection.Buy
        };

        // Act
        var isValid = _riskManager.ValidateOrder(order);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void RiskManager_ValidatePositionSize_WithValidSize_ShouldReturnTrue()
    {
        // Arrange
        var security = new Security { Code = "AAPL" };

        // Act
        var isValid = _riskManager.ValidatePositionSize(security, 500_000m);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void RiskManager_ValidatePositionSize_WithExcessiveSize_ShouldReturnFalse()
    {
        // Arrange
        var security = new Security { Code = "AAPL" };

        // Act
        var isValid = _riskManager.ValidatePositionSize(security, 1_500_000m);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void RiskManager_IsDrawdownLimitBreached_WithExcessiveDrawdown_ShouldReturnTrue()
    {
        // Act
        var isBreached = _riskManager.IsDrawdownLimitBreached(0.15m); // 15% > 10% default limit

        // Assert
        Assert.True(isBreached);
    }

    [Fact]
    public void RiskManager_IsDrawdownLimitBreached_WithAcceptableDrawdown_ShouldReturnFalse()
    {
        // Act
        var isBreached = _riskManager.IsDrawdownLimitBreached(0.05m); // 5% < 10% default limit

        // Assert
        Assert.False(isBreached);
    }

    [Fact]
    public void RiskManager_IsDailyLossLimitBreached_WithExcessiveLoss_ShouldReturnTrue()
    {
        // Act
        var isBreached = _riskManager.IsDailyLossLimitBreached(-60_000m); // Loss > 50k default limit

        // Assert
        Assert.True(isBreached);
    }

    [Fact]
    public void RiskManager_IsDailyLossLimitBreached_WithAcceptableLoss_ShouldReturnFalse()
    {
        // Act
        var isBreached = _riskManager.IsDailyLossLimitBreached(-30_000m); // Loss < 50k default limit

        // Assert
        Assert.False(isBreached);
    }

    [Fact]
    public void RiskManager_RecordViolation_ShouldStoreViolation()
    {
        // Arrange
        var violation = RiskViolation.DrawdownExceeded(0.15m, 0.10m);

        // Act
        _riskManager.RecordViolation(violation);
        var violations = _riskManager.GetRecentViolations(1);

        // Assert
        Assert.Single(violations);
        Assert.Equal(RiskViolationType.MaxDrawdownExceeded, violations[0].ViolationType);
        Assert.Equal(0.15m, violations[0].CurrentValue);
        Assert.Equal(0.10m, violations[0].Threshold);
    }

    [Fact]
    public void RiskManager_GetRecentViolations_ShouldReturnMostRecent()
    {
        // Arrange
        var violation1 = RiskViolation.DrawdownExceeded(0.11m, 0.10m);
        var violation2 = RiskViolation.DailyLossExceeded(-60_000m, -50_000m);
        var violation3 = RiskViolation.PositionSizeExceeded("AAPL", 1_500_000m, 1_000_000m);

        // Act
        _riskManager.RecordViolation(violation1);
        _riskManager.RecordViolation(violation2);
        _riskManager.RecordViolation(violation3);

        var recentViolations = _riskManager.GetRecentViolations(2);

        // Assert
        Assert.Equal(2, recentViolations.Count);
        Assert.Equal(RiskViolationType.DailyLossLimitExceeded, recentViolations[0].ViolationType);
        Assert.Equal(RiskViolationType.PositionSizeExceeded, recentViolations[1].ViolationType);
    }

    [Fact]
    public void RiskManager_ResetDaily_ShouldClearDailyCounters()
    {
        // Arrange
        _riskManager.IsDailyLossLimitBreached(-30_000m); // Record some daily loss

        // Act
        _riskManager.ResetDaily();

        // Assert
        // After reset, the same loss should not trigger limit breach
        var isBreached = _riskManager.IsDailyLossLimitBreached(-30_000m);
        Assert.False(isBreached);
    }

    [Fact]
    public async Task RiskManager_EmergencyStopAsync_ShouldTriggerEmergencyProtocol()
    {
        // Act
        await _riskManager.EmergencyStopAsync();
        var violations = _riskManager.GetRecentViolations(1);

        // Assert
        Assert.Single(violations);
        Assert.Equal(RiskViolationType.EmergencyStop, violations[0].ViolationType);
        Assert.Equal(RiskSeverity.Emergency, violations[0].Severity);
    }

    [Fact]
    public void RiskManager_Dispose_ShouldCleanUpResources()
    {
        // Arrange
        var violation = RiskViolation.DrawdownExceeded(0.15m, 0.10m);
        _riskManager.RecordViolation(violation);

        // Act
        _riskManager.Dispose();

        // Assert - Should not throw
        var violations = _riskManager.GetRecentViolations(10);
        Assert.Empty(violations); // Should be cleared after disposal
    }

    [Theory]
    [InlineData(0.05, false)] // 5% drawdown - OK
    [InlineData(0.10, false)] // 10% drawdown - At limit
    [InlineData(0.15, true)]  // 15% drawdown - Breach
    [InlineData(0.25, true)]  // 25% drawdown - Major breach
    public void RiskManager_DrawdownValidation_ShouldBehavePredictably(decimal drawdown, bool shouldBreach)
    {
        // Act
        var isBreached = _riskManager.IsDrawdownLimitBreached(drawdown);

        // Assert
        Assert.Equal(shouldBreach, isBreached);
    }

    [Theory]
    [InlineData(-10_000, false)]  // Small loss - OK
    [InlineData(-50_000, false)]  // At limit - OK
    [InlineData(-60_000, true)]   // Over limit - Breach
    [InlineData(-100_000, true)]  // Large loss - Breach
    public void RiskManager_DailyLossValidation_ShouldBehavePredictably(decimal dailyPnL, bool shouldBreach)
    {
        // Act
        var isBreached = _riskManager.IsDailyLossLimitBreached(dailyPnL);

        // Assert
        Assert.Equal(shouldBreach, isBreached);
    }
}

/// <summary>
/// Integration tests for RiskManager with multiple violations
/// </summary>
public class RiskManagerIntegrationTests
{
    [Fact]
    public void RiskManager_MultipleViolations_ShouldIncreaseRiskLevel()
    {
        // Arrange
        var riskManager = new RiskManager(NullLogger<RiskManager>.Instance);
        var initialRiskLevel = riskManager.CurrentRiskLevel;

        // Act
        riskManager.IsDrawdownLimitBreached(0.15m); // Trigger drawdown violation
        riskManager.IsDailyLossLimitBreached(-60_000m); // Trigger daily loss violation

        var finalRiskLevel = riskManager.CurrentRiskLevel;

        // Assert
        Assert.True(finalRiskLevel > initialRiskLevel, "Risk level should increase after violations");
    }

    [Fact]
    public void RiskManager_HighRiskLevel_ShouldTriggerRiskLimitBreached()
    {
        // Arrange
        var riskManager = new RiskManager(NullLogger<RiskManager>.Instance);

        // Act - Generate multiple critical violations to increase risk level
        for (int i = 0; i < 5; i++)
        {
            riskManager.IsDrawdownLimitBreached(0.20m + i * 0.01m);
        }

        // Assert
        Assert.True(riskManager.IsRiskLimitBreached, "Risk limit should be breached with high risk level");
    }

    [Fact]
    public void RiskManager_ViolationHistory_ShouldMaintainCircularBuffer()
    {
        // Arrange
        var riskManager = new RiskManager(NullLogger<RiskManager>.Instance, violationHistorySize: 3);

        // Act - Add more violations than buffer size
        riskManager.RecordViolation(RiskViolation.DrawdownExceeded(0.11m, 0.10m));
        riskManager.RecordViolation(RiskViolation.DrawdownExceeded(0.12m, 0.10m));
        riskManager.RecordViolation(RiskViolation.DrawdownExceeded(0.13m, 0.10m));
        riskManager.RecordViolation(RiskViolation.DrawdownExceeded(0.14m, 0.10m)); // Should replace oldest

        var violations = riskManager.GetRecentViolations(10);

        // Assert
        Assert.Equal(3, violations.Count); // Should only keep 3 most recent
        Assert.Equal(0.12m, violations[0].CurrentValue); // Oldest retained
        Assert.Equal(0.13m, violations[1].CurrentValue);
        Assert.Equal(0.14m, violations[2].CurrentValue); // Most recent
    }
}*/