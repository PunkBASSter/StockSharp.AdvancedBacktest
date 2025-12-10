using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public interface IPositionSizer
{
    decimal Calculate(decimal price, decimal? atr, Portfolio portfolio);
}
