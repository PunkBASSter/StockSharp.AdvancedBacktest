using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public class ATRBasedPositionSizer : IPositionSizer
{
    private readonly decimal _equityPercentage;
    private readonly decimal _stopLossATRMultiplier;

    public ATRBasedPositionSizer(decimal equityPercentage, decimal stopLossATRMultiplier)
    {
        if (equityPercentage <= 0 || equityPercentage > 100)
            throw new ArgumentException("Equity percentage must be between 0 and 100", nameof(equityPercentage));

        if (stopLossATRMultiplier <= 0)
            throw new ArgumentException("Stop loss ATR multiplier must be greater than zero", nameof(stopLossATRMultiplier));

        _equityPercentage = equityPercentage;
        _stopLossATRMultiplier = stopLossATRMultiplier;
    }

    public decimal Calculate(decimal price, decimal? atr, Portfolio portfolio)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        if (!atr.HasValue)
            throw new ArgumentException("ATR value is required for ATR-based position sizing", nameof(atr));

        if (atr.Value <= 0)
            throw new ArgumentException("ATR value must be greater than zero", nameof(atr));

        var equity = portfolio?.CurrentValue ?? portfolio?.BeginValue ?? 0;

        if (equity <= 0)
            throw new InvalidOperationException("Portfolio equity must be greater than zero for ATRBased sizing");

        var riskAmount = equity * (_equityPercentage / 100m);
        var riskPerShare = atr.Value * _stopLossATRMultiplier;

        if (riskPerShare <= 0)
            throw new InvalidOperationException("Risk per share must be greater than zero");

        return riskAmount / riskPerShare;
    }
}
