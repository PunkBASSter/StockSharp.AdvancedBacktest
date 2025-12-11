using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.TakeProfit;

/// <summary>
/// Risk/Reward ratio-based take-profit calculator
/// </summary>
public class RiskRewardTakeProfit : ITakeProfitCalculator
{
    private readonly decimal _riskRewardRatio;

    public RiskRewardTakeProfit(decimal riskRewardRatio)
    {
        if (riskRewardRatio <= 0)
            throw new ArgumentException("Risk/reward ratio must be greater than zero", nameof(riskRewardRatio));

        _riskRewardRatio = riskRewardRatio;
    }

    public decimal Calculate(Sides side, decimal entryPrice, decimal stopLoss, decimal? atr)
    {
        if (entryPrice <= 0)
            throw new ArgumentException("Entry price must be greater than zero", nameof(entryPrice));

        if (stopLoss <= 0)
            throw new ArgumentException("Stop-loss must be greater than zero", nameof(stopLoss));

        var risk = Math.Abs(entryPrice - stopLoss);

        var takeProfit = side == Sides.Buy
            ? entryPrice + (risk * _riskRewardRatio)
            : entryPrice - (risk * _riskRewardRatio);

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
