using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;

/// <summary>
/// ATR-based take-profit calculator
/// </summary>
public class ATRTakeProfit : ITakeProfitCalculator
{
    private readonly decimal _atrMultiplier;

    public ATRTakeProfit(decimal atrMultiplier)
    {
        if (atrMultiplier <= 0)
            throw new ArgumentException("ATR multiplier must be greater than zero", nameof(atrMultiplier));

        _atrMultiplier = atrMultiplier;
    }

    public decimal Calculate(Sides side, decimal entryPrice, decimal stopLoss, decimal? atr)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (!atr.HasValue)
            throw new ArgumentException("ATR value is required for ATR-based take profit", nameof(atr));

        if (atr.Value <= 0)
            throw new ArgumentException("ATR value must be greater than zero", nameof(atr));

        var takeProfit = side == Sides.Buy
            ? entryPrice + (atr.Value * _atrMultiplier)
            : entryPrice - (atr.Value * _atrMultiplier);

        ValidateTakeProfit(side, entryPrice, takeProfit);
        return takeProfit;
    }

    private void ValidateTakeProfit(Sides side, decimal entryPrice, decimal takeProfit)
    {
        if (takeProfit <= 0)
            throw new InvalidOperationException("Take-profit must be greater than zero");

        if (side == Sides.Buy && takeProfit <= entryPrice)
            throw new InvalidOperationException(
                $"For long position, take-profit ({takeProfit}) must be above entry price ({entryPrice})");

        if (side == Sides.Sell && takeProfit >= entryPrice)
            throw new InvalidOperationException(
                $"For short position, take-profit ({takeProfit}) must be below entry price ({entryPrice})");
    }
}
