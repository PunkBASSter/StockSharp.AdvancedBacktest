using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;

/// <summary>
/// Interface for take-profit calculators
/// </summary>
public interface ITakeProfitCalculator
{
    /// <summary>
    /// Calculate take-profit level
    /// </summary>
    /// <param name="side">Buy or Sell side</param>
    /// <param name="entryPrice">Entry price of the position</param>
    /// <param name="stopLoss">Stop-loss price level (used for risk/reward ratio calculation)</param>
    /// <param name="atr">Average True Range value (optional, used for ATR-based take profit)</param>
    /// <returns>Take-profit price level</returns>
    decimal Calculate(Sides side, decimal entryPrice, decimal stopLoss, decimal? atr);
}
