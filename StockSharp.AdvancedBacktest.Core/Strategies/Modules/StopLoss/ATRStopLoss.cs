using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.StopLoss;

/// <summary>
/// ATR-based stop-loss calculator
/// </summary>
public class ATRStopLoss : IStopLossCalculator
{
    private readonly decimal _atrMultiplier;

    public ATRStopLoss(decimal atrMultiplier)
    {
        if (atrMultiplier <= 0)
            throw new ArgumentException("ATR multiplier must be greater than zero", nameof(atrMultiplier));

        _atrMultiplier = atrMultiplier;
    }

    public decimal Calculate(Sides side, decimal entryPrice, decimal? atr)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (!atr.HasValue)
            throw new ArgumentException("ATR value is required for ATR-based stop loss", nameof(atr));

        if (atr.Value <= 0)
            throw new ArgumentException("ATR value must be greater than zero", nameof(atr));

        var stopLoss = side == Sides.Buy
            ? entryPrice - (atr.Value * _atrMultiplier)
            : entryPrice + (atr.Value * _atrMultiplier);

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
