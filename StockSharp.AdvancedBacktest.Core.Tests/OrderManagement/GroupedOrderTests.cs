using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class GroupedOrderTests
{
    [Fact]
    public void Constructor_CreatesOrderWithPendingState()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        Assert.Equal("order1", order.OrderId);
        Assert.Equal(GroupedOrderRole.Opening, order.Role);
        Assert.Equal(100m, order.Price);
        Assert.Equal(10m, order.Volume);
        Assert.Equal(0m, order.FilledVolume);
        Assert.Equal(OrderTypes.Limit, order.OrderType);
        Assert.Equal(GroupedOrderState.Pending, order.State);
        Assert.Null(order.BrokerOrder);
        Assert.True(order.CreatedAt <= DateTime.UtcNow);
        Assert.Null(order.FilledAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsOnNonPositiveVolume(decimal volume)
    {
        Assert.Throws<ArgumentException>(() => new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: volume,
            orderType: OrderTypes.Limit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_ThrowsOnNonPositivePriceForLimitOrders(decimal price)
    {
        Assert.Throws<ArgumentException>(() => new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: price,
            volume: 10m,
            orderType: OrderTypes.Limit));
    }

    [Fact]
    public void Constructor_AllowsZeroPriceForMarketOrders()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Closing,
            price: 0m,
            volume: 10m,
            orderType: OrderTypes.Market);

        Assert.Equal(0m, order.Price);
        Assert.Equal(OrderTypes.Market, order.OrderType);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyOrderId()
    {
        Assert.Throws<ArgumentException>(() => new GroupedOrder(
            orderId: "",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit));
    }

    [Fact]
    public void Constructor_ThrowsOnNullOrderId()
    {
        Assert.Throws<ArgumentNullException>(() => new GroupedOrder(
            orderId: null!,
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit));
    }

    [Theory]
    [InlineData(GroupedOrderState.Active)]
    [InlineData(GroupedOrderState.PartiallyFilled)]
    [InlineData(GroupedOrderState.Filled)]
    [InlineData(GroupedOrderState.Cancelled)]
    [InlineData(GroupedOrderState.Rejected)]
    public void SetState_UpdatesState(GroupedOrderState newState)
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        order.SetState(newState);

        Assert.Equal(newState, order.State);
    }

    [Fact]
    public void AddFilledVolume_IncreasesFilledVolume()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        order.AddFilledVolume(3m);
        Assert.Equal(3m, order.FilledVolume);

        order.AddFilledVolume(2m);
        Assert.Equal(5m, order.FilledVolume);
    }

    [Fact]
    public void AddFilledVolume_ThrowsWhenExceedsVolume()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        order.AddFilledVolume(8m);
        Assert.Throws<InvalidOperationException>(() => order.AddFilledVolume(5m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddFilledVolume_ThrowsOnNonPositiveVolume(decimal fillVolume)
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        Assert.Throws<ArgumentException>(() => order.AddFilledVolume(fillVolume));
    }

    [Fact]
    public void MarkFilled_SetsFilledAtAndState()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        var beforeFill = DateTime.UtcNow;
        order.AddFilledVolume(10m);
        order.MarkFilled();
        var afterFill = DateTime.UtcNow;

        Assert.Equal(GroupedOrderState.Filled, order.State);
        Assert.NotNull(order.FilledAt);
        Assert.InRange(order.FilledAt.Value, beforeFill, afterFill);
    }

    [Fact]
    public void RemainingVolume_CalculatesCorrectly()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        Assert.Equal(10m, order.RemainingVolume);

        order.AddFilledVolume(3m);
        Assert.Equal(7m, order.RemainingVolume);

        order.AddFilledVolume(7m);
        Assert.Equal(0m, order.RemainingVolume);
    }

    [Fact]
    public void IsFilled_ReturnsTrueWhenCompletelyFilled()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        Assert.False(order.IsFilled);

        order.AddFilledVolume(10m);
        Assert.True(order.IsFilled);
    }

    [Fact]
    public void IsPartiallyFilled_ReturnsTrueWhenPartiallyFilled()
    {
        var order = new GroupedOrder(
            orderId: "order1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 10m,
            orderType: OrderTypes.Limit);

        Assert.False(order.IsPartiallyFilled);

        order.AddFilledVolume(5m);
        Assert.True(order.IsPartiallyFilled);

        order.AddFilledVolume(5m);
        Assert.False(order.IsPartiallyFilled); // Fully filled is not partially filled
    }
}
