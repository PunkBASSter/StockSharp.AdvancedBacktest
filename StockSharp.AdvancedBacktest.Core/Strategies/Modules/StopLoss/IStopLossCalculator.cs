using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;

/// <summary>
/// Interface for stop-loss calculators
/// </summary>
public interface IStopLossCalculator
{
    /// <summary>
    /// Calculate stop-loss level
    /// </summary>
    /// <param name="side">Buy or Sell side</param>
    /// <param name="entryPrice">Entry price of the position</param>
    /// <param name="atr">Average True Range value (optional, used for ATR-based stop loss)</param>
    /// <returns>Stop-loss price level</returns>
    decimal Calculate(Sides side, decimal entryPrice, decimal? atr);
}
