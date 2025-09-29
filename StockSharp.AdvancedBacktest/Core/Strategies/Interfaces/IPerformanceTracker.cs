using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;

public interface IPerformanceTracker : IDisposable
{
    decimal CurrentValue { get; }
    decimal TotalReturn { get; }
    decimal SharpeRatio { get; }
    decimal MaxDrawdown { get; }
    decimal CurrentDrawdown { get; }
    decimal WinRate { get; }
    int TotalTrades { get; }
    int WinningTrades { get; }
    bool IsConsistent { get; }

    void RecordTrade(Trade trade);
    void UpdatePortfolioValue(decimal value, DateTimeOffset timestamp);
    decimal CalculateVolatility(int periods = 252);
    PerformanceSnapshot GetSnapshot();
    ImmutableArray<PerformanceSnapshot> GetHistory(DateTimeOffset? from = null, DateTimeOffset? to = null);
    void Reset();
}