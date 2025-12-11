using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;

/// <summary>
/// Percentage-based stop-loss calculator
/// </summary>
public class PercentageStopLoss : IStopLossCalculator
{
    private readonly decimal _percentage;

    public PercentageStopLoss(decimal percentage)
    {
        if (percentage <= 0 || percentage >= 100)
            throw new ArgumentException("Stop loss percentage must be between 0 and 100", nameof(percentage));

        _percentage = percentage;
    }

    public decimal Calculate(Sides side, decimal entryPrice, decimal? atr)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        var stopLoss = side == Sides.Buy
            ? entryPrice * (1 - _percentage / 100m)
            : entryPrice * (1 + _percentage / 100m);

        ValidateStopLoss(side, entryPrice, stopLoss);
        return stopLoss;
    }

    private void ValidateStopLoss(Sides side, decimal entryPrice, decimal stopLoss)
    {
        if (stopLoss <= 0)
            throw new InvalidOperationException("Stop-loss must be greater than zero");

        if (side == Sides.Buy && stopLoss >= entryPrice)
            throw new InvalidOperationException(
                $"For long position, stop-loss ({stopLoss}) must be below entry price ({entryPrice})");

        if (side == Sides.Sell && stopLoss <= entryPrice)
            throw new InvalidOperationException(
                $"For short position, stop-loss ({stopLoss}) must be above entry price ({entryPrice})");
    }
}
