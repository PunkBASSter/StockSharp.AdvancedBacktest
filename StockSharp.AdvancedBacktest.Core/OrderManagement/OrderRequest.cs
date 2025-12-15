using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public record ProtectivePair(decimal StopLossPrice, decimal TakeProfitPrice, decimal? Volume, OrderTypes OrderType = OrderTypes.Limit);
public record OrderRequest(Order Order, List<ProtectivePair> ProtectivePairs);
