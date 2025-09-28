using StockSharp.BusinessEntities;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

/// <summary>
/// Immutable record for trade execution event data
/// </summary>
/// <param name="OriginalTrade">The original StockSharp trade</param>
/// <param name="StrategyParameters">Snapshot of strategy parameters at execution time</param>
/// <param name="Timestamp">Execution timestamp</param>
/// <param name="PortfolioSnapshot">Portfolio state at execution time</param>
public record TradeExecutionData(
    [property: JsonIgnore] Trade OriginalTrade,
    ImmutableDictionary<string, object?> StrategyParameters,
    DateTimeOffset Timestamp,
    PortfolioSnapshot PortfolioSnapshot
)
{
    /// <summary>
    /// Trade ID for serialization
    /// </summary>
    [JsonPropertyName("tradeId")]
    public long TradeId => OriginalTrade.Id;

    /// <summary>
    /// Security code for serialization
    /// </summary>
    [JsonPropertyName("securityCode")]
    public string SecurityCode => OriginalTrade.Security.Code;

    /// <summary>
    /// Trade price
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price => OriginalTrade.Price;

    /// <summary>
    /// Trade volume
    /// </summary>
    [JsonPropertyName("volume")]
    public decimal Volume => OriginalTrade.Volume;

    /// <summary>
    /// Trade side (Buy/Sell)
    /// </summary>
    [JsonPropertyName("side")]
    public string Side => OriginalTrade.OrderDirection.ToString() ?? "Unknown";

    /// <summary>
    /// Commission paid (calculated separately)
    /// </summary>
    [JsonPropertyName("commission")]
    public decimal? Commission => null; // Would be calculated based on strategy commission rules

    /// <summary>
    /// Profit/Loss from this trade (calculated separately)
    /// </summary>
    [JsonPropertyName("pnl")]
    public decimal? PnL => null; // Would be calculated based on position tracking

    /// <summary>
    /// Position size after this trade (from portfolio snapshot)
    /// </summary>
    [JsonPropertyName("positionSize")]
    public decimal? PositionSize => null; // Would be extracted from portfolio snapshot
}

/// <summary>
/// Portfolio snapshot at a point in time
/// </summary>
/// <param name="TotalValue">Total portfolio value</param>
/// <param name="Cash">Available cash</param>
/// <param name="UnrealizedPnL">Unrealized profit/loss</param>
/// <param name="RealizedPnL">Realized profit/loss</param>
/// <param name="Timestamp">Snapshot timestamp</param>
public readonly record struct PortfolioSnapshot(
    [property: JsonPropertyName("totalValue")] decimal TotalValue,
    [property: JsonPropertyName("cash")] decimal Cash,
    [property: JsonPropertyName("unrealizedPnL")] decimal UnrealizedPnL,
    [property: JsonPropertyName("realizedPnL")] decimal RealizedPnL,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp
);