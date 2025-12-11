using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderGroupTests
{
    private static GroupedOrder CreateOpeningOrder(decimal volume = 100m) => new(
        orderId: "open1",
        role: GroupedOrderRole.Opening,
        price: 100m,
        volume: volume,
        orderType: OrderTypes.Limit);

    private static GroupedOrder CreateClosingOrder(string id, decimal volume, decimal price = 110m) => new(
        orderId: id,
        role: GroupedOrderRole.Closing,
        price: price,
        volume: volume,
        orderType: OrderTypes.Limit);

    [Fact]
    public void Constructor_CreatesGroupInPendingState()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 50m),
            CreateClosingOrder("close2", 50m, 120m)
        };

        var group = new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: closingOrders);

        Assert.Equal("group1", group.GroupId);
        Assert.Equal("SBER@TQBR", group.SecurityId);
        Assert.Equal(Sides.Buy, group.Direction);
        Assert.Equal(OrderGroupState.Pending, group.State);
        Assert.Same(openingOrder, group.OpeningOrder);
        Assert.Equal(2, group.ClosingOrders.Count);
        Assert.True(group.CreatedAt <= DateTime.UtcNow);
        Assert.Null(group.ActivatedAt);
        Assert.Null(group.CompletedAt);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyGroupId()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };

        Assert.Throws<ArgumentException>(() => new OrderGroup(
            groupId: "",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: closingOrders));
    }

    [Fact]
    public void Constructor_ThrowsOnNullGroupId()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };

        Assert.Throws<ArgumentNullException>(() => new OrderGroup(
            groupId: null!,
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: closingOrders));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptySecurityId()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };

        Assert.Throws<ArgumentException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: closingOrders));
    }

    [Fact]
    public void Constructor_ThrowsOnNullOpeningOrder()
    {
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };

        Assert.Throws<ArgumentNullException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: null!,
            closingOrders: closingOrders));
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyClosingOrders()
    {
        var openingOrder = CreateOpeningOrder();

        Assert.Throws<ArgumentException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: new List<GroupedOrder>()));
    }

    [Fact]
    public void Constructor_ThrowsOnNullClosingOrders()
    {
        var openingOrder = CreateOpeningOrder();

        Assert.Throws<ArgumentNullException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: null!));
    }

    [Fact]
    public void Constructor_ThrowsOnOpeningOrderWithClosingRole()
    {
        var wrongOrder = CreateClosingOrder("wrong", 100m);
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };

        Assert.Throws<ArgumentException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: wrongOrder,
            closingOrders: closingOrders));
    }

    [Fact]
    public void Constructor_ThrowsOnClosingOrderWithOpeningRole()
    {
        var openingOrder = CreateOpeningOrder();
        var wrongClosing = new GroupedOrder("wrong", GroupedOrderRole.Opening, 110m, 100m, OrderTypes.Limit);
        var closingOrders = new List<GroupedOrder> { wrongClosing };

        Assert.Throws<ArgumentException>(() => new OrderGroup(
            groupId: "group1",
            securityId: "SBER@TQBR",
            direction: Sides.Buy,
            openingOrder: openingOrder,
            closingOrders: closingOrders));
    }

    [Fact]
    public void TotalClosingVolume_SumsAllClosingOrderVolumes()
    {
        var openingOrder = CreateOpeningOrder(100m);
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 30m),
            CreateClosingOrder("close2", 30m, 115m),
            CreateClosingOrder("close3", 40m, 120m)
        };

        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        Assert.Equal(100m, group.TotalClosingVolume);
    }

    [Fact]
    public void FilledClosingVolume_SumsFilledVolumes()
    {
        var openingOrder = CreateOpeningOrder(100m);
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 30m),
            CreateClosingOrder("close2", 30m, 115m),
            CreateClosingOrder("close3", 40m, 120m)
        };

        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        Assert.Equal(0m, group.FilledClosingVolume);

        closingOrders[0].AddFilledVolume(30m);
        Assert.Equal(30m, group.FilledClosingVolume);

        closingOrders[1].AddFilledVolume(15m);
        Assert.Equal(45m, group.FilledClosingVolume);
    }

    [Fact]
    public void RemainingVolume_CalculatesFilledOpeningMinusFilledClosing()
    {
        var openingOrder = CreateOpeningOrder(100m);
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 50m),
            CreateClosingOrder("close2", 50m, 120m)
        };

        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        // No fills yet
        Assert.Equal(0m, group.RemainingVolume);

        // Opening fills
        openingOrder.AddFilledVolume(100m);
        Assert.Equal(100m, group.RemainingVolume);

        // Partial closing fills
        closingOrders[0].AddFilledVolume(30m);
        Assert.Equal(70m, group.RemainingVolume);

        // Complete closing fills
        closingOrders[0].AddFilledVolume(20m);
        closingOrders[1].AddFilledVolume(50m);
        Assert.Equal(0m, group.RemainingVolume);
    }

    [Fact]
    public void IsVolumeMatched_ReturnsTrueWhenVolumesMatch()
    {
        var openingOrder = CreateOpeningOrder(100m);
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 50m),
            CreateClosingOrder("close2", 50m, 120m)
        };

        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        Assert.True(group.IsVolumeMatched);
    }

    [Fact]
    public void IsVolumeMatched_ReturnsFalseWhenVolumesMismatch()
    {
        var openingOrder = CreateOpeningOrder(100m);
        var closingOrders = new List<GroupedOrder>
        {
            CreateClosingOrder("close1", 40m),
            CreateClosingOrder("close2", 40m, 120m)
        };

        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        Assert.False(group.IsVolumeMatched);
    }

    [Theory]
    [InlineData(OrderGroupState.Active)]
    [InlineData(OrderGroupState.Closing)]
    [InlineData(OrderGroupState.Completed)]
    [InlineData(OrderGroupState.Cancelled)]
    public void SetState_UpdatesState(OrderGroupState newState)
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        group.SetState(newState);

        Assert.Equal(newState, group.State);
    }

    [Fact]
    public void MarkActivated_SetsActivatedAtAndState()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var beforeActivation = DateTime.UtcNow;
        group.MarkActivated();
        var afterActivation = DateTime.UtcNow;

        Assert.Equal(OrderGroupState.Active, group.State);
        Assert.NotNull(group.ActivatedAt);
        Assert.InRange(group.ActivatedAt.Value, beforeActivation, afterActivation);
    }

    [Fact]
    public void MarkCompleted_SetsCompletedAtAndState()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var beforeCompletion = DateTime.UtcNow;
        group.MarkCompleted();
        var afterCompletion = DateTime.UtcNow;

        Assert.Equal(OrderGroupState.Completed, group.State);
        Assert.NotNull(group.CompletedAt);
        Assert.InRange(group.CompletedAt.Value, beforeCompletion, afterCompletion);
    }

    [Fact]
    public void MarkCancelled_SetsCompletedAtAndState()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var beforeCancellation = DateTime.UtcNow;
        group.MarkCancelled();
        var afterCancellation = DateTime.UtcNow;

        Assert.Equal(OrderGroupState.Cancelled, group.State);
        Assert.NotNull(group.CompletedAt);
        Assert.InRange(group.CompletedAt.Value, beforeCancellation, afterCancellation);
    }

    [Fact]
    public void GetOrderById_ReturnsOpeningOrder()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var foundOrder = group.GetOrderById("open1");

        Assert.Same(openingOrder, foundOrder);
    }

    [Fact]
    public void GetOrderById_ReturnsClosingOrder()
    {
        var openingOrder = CreateOpeningOrder();
        var close1 = CreateClosingOrder("close1", 50m);
        var close2 = CreateClosingOrder("close2", 50m, 120m);
        var closingOrders = new List<GroupedOrder> { close1, close2 };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var foundOrder = group.GetOrderById("close2");

        Assert.Same(close2, foundOrder);
    }

    [Fact]
    public void GetOrderById_ReturnsNullForUnknownId()
    {
        var openingOrder = CreateOpeningOrder();
        var closingOrders = new List<GroupedOrder> { CreateClosingOrder("close1", 100m) };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        var foundOrder = group.GetOrderById("unknown");

        Assert.Null(foundOrder);
    }

    [Fact]
    public void AllClosingOrdersFilled_ReturnsTrueWhenAllFilled()
    {
        var openingOrder = CreateOpeningOrder();
        var close1 = CreateClosingOrder("close1", 50m);
        var close2 = CreateClosingOrder("close2", 50m, 120m);
        var closingOrders = new List<GroupedOrder> { close1, close2 };
        var group = new OrderGroup("group1", "SBER@TQBR", Sides.Buy, openingOrder, closingOrders);

        Assert.False(group.AllClosingOrdersFilled);

        close1.AddFilledVolume(50m);
        Assert.False(group.AllClosingOrdersFilled);

        close2.AddFilledVolume(50m);
        Assert.True(group.AllClosingOrdersFilled);
    }
}
