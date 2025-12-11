using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public class PercentEquityPositionSizer : IPositionSizer
{
    private readonly decimal _equityPercentage;

    public PercentEquityPositionSizer(decimal equityPercentage)
    {
        if (equityPercentage <= 0 || equityPercentage > 100)
            throw new ArgumentException("Equity percentage must be between 0 and 100", nameof(equityPercentage));

        _equityPercentage = equityPercentage;
    }

    public decimal Calculate(decimal price, decimal? atr, Portfolio portfolio)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        var equity = portfolio?.CurrentValue ?? portfolio?.BeginValue ?? 0;

        if (equity <= 0)
            throw new InvalidOperationException("Portfolio equity must be greater than zero for PercentOfEquity sizing");

        var riskAmount = equity * (_equityPercentage / 100m);
        return riskAmount / price;
    }
}
