using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class IStrategyOrderOperationsContractTests
{
    [Fact]
    public void Interface_DefinesPlaceOrderMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("PlaceOrder");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_DefinesCancelOrderMethod()
    {
        var method = typeof(IStrategyOrderOperations).GetMethod("CancelOrder");

        Assert.NotNull(method);
    }

    [Fact]
    public void Interface_HasOnlyTwoMethods()
    {
        var methods = typeof(IStrategyOrderOperations).GetMethods();

        Assert.Equal(2, methods.Length);
    }
}

public class OrderPositionManagerConstructorTests
{
    [Fact]
    public void Constructor_ThrowsOnNullStrategy()
    {
        var security = new Security { Id = "TEST@TEST", PriceStep = 0.01m };
        Assert.Throws<ArgumentNullException>(() => new OrderPositionManager(null!, security, "test"));
    }

    [Fact]
    public void Constructor_ThrowsOnNullSecurity()
    {
        var strategy = new TestStrategy();
        Assert.Throws<ArgumentNullException>(() => new OrderPositionManager(strategy, null!, "test"));
    }

    [Fact]
    public void Constructor_TakesThreeParameters()
    {
        var ctorParams = typeof(OrderPositionManager).GetConstructors()[0].GetParameters();

        Assert.Equal(3, ctorParams.Length);
        Assert.Equal("strategy", ctorParams[0].Name);
        Assert.Equal("security", ctorParams[1].Name);
        Assert.Equal("strategyName", ctorParams[2].Name);
    }

    private class TestStrategy : IStrategyOrderOperations
    {
        public Order PlaceOrder(Order order) => order;
        public void CancelOrder(Order order) { }
    }
}
