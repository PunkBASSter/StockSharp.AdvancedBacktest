using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public class FixedPositionSizer : IPositionSizer
{
    private readonly decimal _fixedSize;

    public FixedPositionSizer(decimal fixedSize)
    {
        if (fixedSize <= 0)
            throw new ArgumentException("Fixed position size must be greater than zero", nameof(fixedSize));

        _fixedSize = fixedSize;
    }

    public decimal Calculate(decimal price, decimal? atr, Portfolio portfolio)
    {
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        return _fixedSize;
    }
}
