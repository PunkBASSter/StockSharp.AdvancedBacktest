using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;

/// <summary>
/// Percentage-based take-profit calculator
/// </summary>
public class PercentageTakeProfit : ITakeProfitCalculator
{
    private readonly decimal _percentage;

    public PercentageTakeProfit(decimal percentage)
    {
        if (percentage <= 0)
            throw new ArgumentException("Take profit percentage must be greater than zero", nameof(percentage));

        _percentage = percentage;
    }

    public decimal Calculate(Sides side, decimal entryPrice, decimal stopLoss, decimal? atr)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        var takeProfit = side == Sides.Buy
            ? entryPrice * (1 + _percentage / 100m)
            : entryPrice * (1 - _percentage / 100m);

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
