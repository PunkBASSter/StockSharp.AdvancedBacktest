using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.OrderManagement;

public record ProtectivePair(decimal StopLossPrice, decimal TakeProfitPrice, decimal? Volume);
public record OrderRequest(Order Order, List<ProtectivePair> ProtectivePairs);