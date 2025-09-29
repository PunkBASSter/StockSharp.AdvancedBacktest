using StockSharp.BusinessEntities;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public record TradeExecutionData(
    [property: JsonIgnore] Trade OriginalTrade,
    ImmutableDictionary<string, object?> StrategyParameters,
    DateTimeOffset Timestamp,
    PortfolioSnapshot PortfolioSnapshot
)
{
    [JsonPropertyName("tradeId")]
    public long TradeId => OriginalTrade.Id;

    [JsonPropertyName("securityCode")]
    public string SecurityCode => OriginalTrade.Security.Code;

    [JsonPropertyName("price")]
    public decimal Price => OriginalTrade.Price;

    [JsonPropertyName("volume")]
    public decimal Volume => OriginalTrade.Volume;

    [JsonPropertyName("side")]
    public string Side => OriginalTrade.OrderDirection.ToString() ?? "Unknown";

    [JsonPropertyName("commission")]
    public decimal? Commission => null; // Would be calculated based on strategy commission rules

    [JsonPropertyName("pnl")]
    public decimal? PnL => null; // Would be calculated based on position tracking

    [JsonPropertyName("positionSize")]
    public decimal? PositionSize => null; // Would be extracted from portfolio snapshot
}

public readonly record struct PortfolioSnapshot(
    [property: JsonPropertyName("totalValue")] decimal TotalValue,
    [property: JsonPropertyName("cash")] decimal Cash,
    [property: JsonPropertyName("unrealizedPnL")] decimal UnrealizedPnL,
    [property: JsonPropertyName("realizedPnL")] decimal RealizedPnL,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp
);