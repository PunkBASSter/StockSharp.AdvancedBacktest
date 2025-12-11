using StockSharp.AdvancedBacktest.OrderManagement;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class IStrategyOrderOperationsContractTests
{
    [Fact]
    public void Interface_DefinesSecurityProperty()
    {
        var property = typeof(IStrategyOrderOperations).GetProperty("Security");

        Assert.NotNull(property);
    }

    [Fact]
    public void Interface_DefinesPositionProperty()
    {
        var property = typeof(IStrategyOrderOperations).GetProperty("Position");

        Assert.NotNull(property);
        Assert.Equal(typeof(decimal), property.PropertyType);
    }

    [Fact]
    public void Interface_DefinesBuyLimitMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("BuyLimit");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_DefinesSellLimitMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("SellLimit");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_DefinesBuyMarketMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("BuyMarket");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_DefinesSellMarketMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("SellMarket");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_DefinesCancelOrderMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("CancelOrder");

        Assert.NotNull(method);
    }
}

public class OrderPositionManagerTests
{
    [Fact]
    public void Constructor_ThrowsOnNullStrategy()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderPositionManager(null!));
    }
}
