using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public record PerformanceSnapshot(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("portfolioValue")] decimal PortfolioValue,
    [property: JsonPropertyName("totalReturn")] decimal TotalReturn,
    [property: JsonPropertyName("sharpeRatio")] decimal SharpeRatio,
    [property: JsonPropertyName("maxDrawdown")] decimal MaxDrawdown,
    [property: JsonPropertyName("currentDrawdown")] decimal CurrentDrawdown,
    [property: JsonPropertyName("winRate")] decimal WinRate,
    [property: JsonPropertyName("totalTrades")] int TotalTrades,
    [property: JsonPropertyName("winningTrades")] int WinningTrades,
    [property: JsonPropertyName("volatility")] decimal Volatility,
    [property: JsonPropertyName("dailyPnL")] decimal DailyPnL
)
{
    /// <summary>
    /// Empty performance snapshot for initialization
    /// </summary>
    public static readonly PerformanceSnapshot Empty = new(
        Timestamp: DateTimeOffset.UtcNow,
        PortfolioValue: 0m,
        TotalReturn: 0m,
        SharpeRatio: 0m,
        MaxDrawdown: 0m,
        CurrentDrawdown: 0m,
        WinRate: 0m,
        TotalTrades: 0,
        WinningTrades: 0,
        Volatility: 0m,
        DailyPnL: 0m
    );

    /// <summary>
    /// Losing trades count
    /// </summary>
    [JsonPropertyName("losingTrades")]
    public int LosingTrades => TotalTrades - WinningTrades;

    /// <summary>
    /// Average win amount (if trades > 0)
    /// </summary>
    [JsonPropertyName("averageWin")]
    public decimal AverageWin => WinningTrades > 0 ? TotalReturn / WinningTrades : 0m;

    /// <summary>
    /// Average loss amount (if trades > 0)
    /// </summary>
    [JsonPropertyName("averageLoss")]
    public decimal AverageLoss => LosingTrades > 0 ? -TotalReturn / LosingTrades : 0m;

    /// <summary>
    /// Profit factor (total wins / total losses)
    /// </summary>
    [JsonPropertyName("profitFactor")]
    public decimal ProfitFactor => AverageLoss != 0m ? Math.Abs(AverageWin / AverageLoss) : 0m;

    /// <summary>
    /// Create a new snapshot with updated values
    /// </summary>
    public PerformanceSnapshot With(
        DateTimeOffset? timestamp = null,
        decimal? portfolioValue = null,
        decimal? totalReturn = null,
        decimal? sharpeRatio = null,
        decimal? maxDrawdown = null,
        decimal? currentDrawdown = null,
        decimal? winRate = null,
        int? totalTrades = null,
        int? winningTrades = null,
        decimal? volatility = null,
        decimal? dailyPnL = null) => this with
        {
            Timestamp = timestamp ?? Timestamp,
            PortfolioValue = portfolioValue ?? PortfolioValue,
            TotalReturn = totalReturn ?? TotalReturn,
            SharpeRatio = sharpeRatio ?? SharpeRatio,
            MaxDrawdown = maxDrawdown ?? MaxDrawdown,
            CurrentDrawdown = currentDrawdown ?? CurrentDrawdown,
            WinRate = winRate ?? WinRate,
            TotalTrades = totalTrades ?? TotalTrades,
            WinningTrades = winningTrades ?? WinningTrades,
            Volatility = volatility ?? Volatility,
            DailyPnL = dailyPnL ?? DailyPnL
        };
}