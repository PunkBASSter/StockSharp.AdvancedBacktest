using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IRiskManager : IDisposable
{
    decimal MaxDrawdownLimit { get; set; }
    decimal MaxPositionSize { get; set; }
    decimal DailyLossLimit { get; set; }
    decimal CurrentRiskLevel { get; }
    bool IsRiskLimitBreached { get; }

    bool ValidateOrder(Order order);
    bool ValidatePositionSize(Security security, decimal volume);
    bool IsDrawdownLimitBreached(decimal currentDrawdown);
    bool IsDailyLossLimitBreached(decimal dailyPnL);
    void RecordViolation(RiskViolation violation);
    IReadOnlyList<RiskViolation> GetRecentViolations(int count = 10);
    void ResetDaily();
    Task EmergencyStopAsync();
}