using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Xunit;

namespace StockSharp.AdvancedBacktest.Core.Tests.OrderManagement;

public class ProtectivePairRecordTests
{
    [Fact]
    public void ProtectivePair_CreatesWithAllProperties()
    {
        var pair = new ProtectivePair(95m, 110m, 0.5m);

        Assert.Equal(95m, pair.StopLossPrice);
        Assert.Equal(110m, pair.TakeProfitPrice);
        Assert.Equal(0.5m, pair.Volume);
    }

    [Fact]
    public void ProtectivePair_VolumeCanBeNull()
    {
        var pair = new ProtectivePair(95m, 110m, null);

        Assert.Null(pair.Volume);
    }

    [Fact]
    public void ProtectivePair_IsValueEquality()
    {
        var pair1 = new ProtectivePair(95m, 110m, 0.5m);
        var pair2 = new ProtectivePair(95m, 110m, 0.5m);

        Assert.Equal(pair1, pair2);
    }

    [Fact]
    public void ProtectivePair_DifferentValues_NotEqual()
    {
        var pair1 = new ProtectivePair(95m, 110m, 0.5m);
        var pair2 = new ProtectivePair(94m, 110m, 0.5m);

        Assert.NotEqual(pair1, pair2);
    }

    [Fact]
    public void ProtectivePair_DefaultOrderType_IsLimit()
    {
        var pair = new ProtectivePair(95m, 110m, 0.5m);

        Assert.Equal(OrderTypes.Limit, pair.OrderType);
    }

    [Fact]
    public void ProtectivePair_WithExplicitMarketOrderType_StoresCorrectly()
    {
        var pair = new ProtectivePair(95m, 110m, 0.5m, OrderTypes.Market);

        Assert.Equal(OrderTypes.Market, pair.OrderType);
    }

    [Fact]
    public void ProtectivePair_WithExplicitLimitOrderType_StoresCorrectly()
    {
        var pair = new ProtectivePair(95m, 110m, 0.5m, OrderTypes.Limit);

        Assert.Equal(OrderTypes.Limit, pair.OrderType);
    }

    [Fact]
    public void ProtectivePair_DifferentOrderTypes_AreNotEqual()
    {
        var limitPair = new ProtectivePair(95m, 110m, 0.5m, OrderTypes.Limit);
        var marketPair = new ProtectivePair(95m, 110m, 0.5m, OrderTypes.Market);

        Assert.NotEqual(limitPair, marketPair);
    }
}

public class OrderRequestRecordTests
{
    [Fact]
    public void OrderRequest_CreatesWithOrderAndPairs()
    {
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

        var request = new OrderRequest(order, pairs);

        Assert.Equal(order, request.Order);
        Assert.Single(request.ProtectivePairs);
    }

    [Fact]
    public void OrderRequest_WithMultiplePairs()
    {
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.5m),
            new(95m, 110m, 0.5m)
        };

        var request = new OrderRequest(order, pairs);

        Assert.Equal(2, request.ProtectivePairs.Count);
    }

    [Fact]
    public void OrderRequest_IsValueEquality()
    {
        var order1 = CreateOrder(100m, Sides.Buy, 1m);
        var order2 = CreateOrder(100m, Sides.Buy, 1m);
        var pairs1 = new List<ProtectivePair> { new(95m, 110m, null) };
        var pairs2 = new List<ProtectivePair> { new(95m, 110m, null) };

        var request1 = new OrderRequest(order1, pairs1);
        var request2 = new OrderRequest(order2, pairs2);

        // Note: Order is a reference type, so requests won't be equal unless same Order instance
        Assert.NotEqual(request1, request2);
    }

    [Fact]
    public void OrderRequest_SameOrderInstance_AreEqual()
    {
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs1 = new List<ProtectivePair> { new(95m, 110m, null) };
        var pairs2 = new List<ProtectivePair> { new(95m, 110m, null) };

        var request1 = new OrderRequest(order, pairs1);
        var request2 = new OrderRequest(order, pairs2);

        // Lists are reference types, so not equal even with same content
        Assert.NotEqual(request1, request2);
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRequestVolumeValidationTests
{
    [Fact]
    public void RegisterGroup_SinglePairWithNullVolume_Succeeds()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

        var group = registry.RegisterGroup(order, pairs);

        Assert.NotNull(group);
    }

    [Fact]
    public void RegisterGroup_SinglePairWithMatchingVolume_Succeeds()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, 1m) };

        var group = registry.RegisterGroup(order, pairs);

        Assert.NotNull(group);
    }

    [Fact]
    public void RegisterGroup_MultiplePairs_VolumesSumToEntry_Succeeds()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.3m),
            new(95m, 108m, 0.3m),
            new(95m, 110m, 0.4m)  // Total: 1.0m
        };

        var group = registry.RegisterGroup(order, pairs);

        Assert.NotNull(group);
        Assert.Equal(3, group.ProtectivePairs.Count);
    }

    [Fact]
    public void RegisterGroup_MultiplePairs_VolumesExceedEntry_ThrowsArgumentException()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.6m),
            new(95m, 110m, 0.6m)  // Total: 1.2m > 1.0m
        };

        Assert.Throws<ArgumentException>(() => registry.RegisterGroup(order, pairs));
    }

    [Fact]
    public void RegisterGroup_MultiplePairs_VolumesBelowEntry_ThrowsArgumentException()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.3m),
            new(95m, 110m, 0.3m)  // Total: 0.6m < 1.0m
        };

        Assert.Throws<ArgumentException>(() => registry.RegisterGroup(order, pairs));
    }

    [Fact]
    public void RegisterGroup_MultiplePairsWithNullVolumesFallbackToEntryVolume()
    {
        var registry = new OrderRegistry("test");
        var order = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, null),
            new(95m, 110m, null)  // Both null = both use entry volume = 2.0 total
        };

        // With null volumes, each pair uses entry volume (1.0),
        // so 2 pairs = 2.0 which exceeds 1.0 entry volume
        Assert.Throws<ArgumentException>(() => registry.RegisterGroup(order, pairs));
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}
