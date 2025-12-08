using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Strategies.Modules.PositionSizing;

public interface IRiskAwarePositionSizer
{
    decimal Calculate(decimal entryPrice, decimal stopLoss, Portfolio portfolio, Security? security = null);
}
