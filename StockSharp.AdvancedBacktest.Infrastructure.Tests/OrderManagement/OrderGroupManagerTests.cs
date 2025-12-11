using StockSharp.AdvancedBacktest.Infrastructure.OrderManagement;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderGroupManagerTests
{
    private readonly MockStrategyOrderOperations _operations;
    private readonly OrderGroupLimits _limits;
    private readonly OrderGroupManager _manager;

    public OrderGroupManagerTests()
    {
        _operations = new MockStrategyOrderOperations();
        _limits = new OrderGroupLimits();
        _manager = new OrderGroupManager(_operations, _limits);
    }

    private static ExtendedTradeSignal CreateValidBuySignal(
        decimal entryPrice = 100m,
        decimal volume = 100m,
        string? groupId = null)
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, volume * 0.5m),
            new(120m, volume * 0.5m)
        };

        return new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: entryPrice,
            entryVolume: volume,
            closingOrders: closingOrders,
            stopLossPrice: 95m,
            groupId: groupId);
    }

    private static ExtendedTradeSignal CreateValidSellSignal(
        decimal entryPrice = 100m,
        decimal volume = 100m,
        string? groupId = null)
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(entryPrice * 0.9m, volume * 0.5m),
            new(entryPrice * 0.8m, volume * 0.5m)
        };

        return new ExtendedTradeSignal(
            direction: Sides.Sell,
            entryPrice: entryPrice,
            entryVolume: volume,
            closingOrders: closingOrders,
            stopLossPrice: entryPrice * 1.05m,
            groupId: groupId);
    }

    #region User Story 1: Create Order Group with Multiple Closing Orders

    [Fact]
    public void CreateOrderGroup_WithValidSignal_ReturnsOrderGroupInPendingState()
    {
        var signal = CreateValidBuySignal();

        var group = _manager.CreateOrderGroup(signal);

        Assert.NotNull(group);
        Assert.Equal(OrderGroupState.Pending, group.State);
        Assert.Equal(Sides.Buy, group.Direction);
        Assert.Equal(_operations.Security.Id, group.SecurityId);
        Assert.NotNull(group.OpeningOrder);
        Assert.Equal(2, group.ClosingOrders.Count);
    }

    [Fact]
    public void CreateOrderGroup_WithValidSignal_PlacesOpeningOrder()
    {
        var signal = CreateValidBuySignal(entryPrice: 100m, volume: 100m);

        var group = _manager.CreateOrderGroup(signal);

        Assert.Single(_operations.Orders);
        var order = _operations.Orders[0];
        Assert.Equal(Sides.Buy, order.Side);
        Assert.Equal(100m, order.Price);
        Assert.Equal(100m, order.Volume);
    }

    [Fact]
    public void CreateOrderGroup_VolumeMismatch_ThrowsWhenThrowIfNotMatchingVolumeTrue()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 40m),
            new(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        Assert.Throws<ArgumentException>(() => _manager.CreateOrderGroup(signal, throwIfNotMatchingVolume: true));
    }

    [Fact]
    public void CreateOrderGroup_VolumeMismatch_SucceedsWhenThrowIfNotMatchingVolumeFalse()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 40m),
            new(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            skipValidation: true);

        var group = _manager.CreateOrderGroup(signal, throwIfNotMatchingVolume: false);

        Assert.NotNull(group);
        Assert.False(group.IsVolumeMatched);
    }

    [Fact]
    public void CreateOrderGroup_DefaultGroupIdFormat_GeneratesExpectedPattern()
    {
        var signal = CreateValidBuySignal(entryPrice: 100m);

        var group = _manager.CreateOrderGroup(signal);

        // GroupId format: {SecurityId}_{DateTimeMs}_{Price}
        Assert.StartsWith(_operations.Security.Id, group.GroupId);
        Assert.Contains("_100", group.GroupId);
    }

    [Fact]
    public void CreateOrderGroup_CustomGroupId_UsesProvidedId()
    {
        var signal = CreateValidBuySignal(groupId: "my-custom-group");

        var group = _manager.CreateOrderGroup(signal);

        Assert.Equal("my-custom-group", group.GroupId);
    }

    [Fact]
    public void CreateOrderGroup_SellSignal_PlacesCorrectOrder()
    {
        var signal = CreateValidSellSignal(entryPrice: 100m, volume: 100m);

        var group = _manager.CreateOrderGroup(signal);

        Assert.Single(_operations.Orders);
        var order = _operations.Orders[0];
        Assert.Equal(Sides.Sell, order.Side);
        Assert.Equal(100m, order.Price);
        Assert.Equal(100m, order.Volume);
    }

    [Fact]
    public void CreateOrderGroup_MultipleClosingOrders_CreatesAllClosingOrderDefinitions()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 30m),
            new(115m, 30m),
            new(120m, 40m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = _manager.CreateOrderGroup(signal);

        Assert.Equal(3, group.ClosingOrders.Count);
        Assert.Equal(30m, group.ClosingOrders[0].Volume);
        Assert.Equal(30m, group.ClosingOrders[1].Volume);
        Assert.Equal(40m, group.ClosingOrders[2].Volume);
    }

    [Fact]
    public void CreateOrderGroup_OpeningOrderHasCorrectRole()
    {
        var signal = CreateValidBuySignal();

        var group = _manager.CreateOrderGroup(signal);

        Assert.Equal(GroupedOrderRole.Opening, group.OpeningOrder.Role);
    }

    [Fact]
    public void CreateOrderGroup_ClosingOrdersHaveCorrectRole()
    {
        var signal = CreateValidBuySignal();

        var group = _manager.CreateOrderGroup(signal);

        Assert.All(group.ClosingOrders, o => Assert.Equal(GroupedOrderRole.Closing, o.Role));
    }

    [Fact]
    public void CreateOrderGroup_OpeningOrderLinkedToBrokerOrder()
    {
        var signal = CreateValidBuySignal();

        var group = _manager.CreateOrderGroup(signal);

        Assert.NotNull(group.OpeningOrder.BrokerOrder);
        Assert.Same(_operations.Orders[0], group.OpeningOrder.BrokerOrder);
    }

    [Fact]
    public void CreateOrderGroup_ClosingOrdersNotYetLinkedToBrokerOrders()
    {
        var signal = CreateValidBuySignal();

        var group = _manager.CreateOrderGroup(signal);

        Assert.All(group.ClosingOrders, o => Assert.Null(o.BrokerOrder));
    }

    #endregion

    #region User Story 3: Multiple Order Groups and Limits

    [Fact]
    public void GetActiveGroups_ReturnsAllActiveGroups()
    {
        var signal1 = CreateValidBuySignal(groupId: "group1");
        var signal2 = CreateValidBuySignal(groupId: "group2");

        _manager.CreateOrderGroup(signal1);
        _manager.CreateOrderGroup(signal2);

        var groups = _manager.GetActiveGroups();

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void GetActiveGroups_WithSecurityFilter_ReturnsOnlyMatchingGroups()
    {
        var ops1 = new MockStrategyOrderOperations("SBER@TQBR");
        var ops2 = new MockStrategyOrderOperations("GAZP@TQBR");
        var manager1 = new OrderGroupManager(ops1, _limits);
        var manager2 = new OrderGroupManager(ops2, _limits);

        var signal1 = CreateValidBuySignal(groupId: "group1");
        var signal2 = CreateValidBuySignal(groupId: "group2");

        manager1.CreateOrderGroup(signal1);

        var groups = manager1.GetActiveGroups("SBER@TQBR");
        Assert.Single(groups);

        var noGroups = manager1.GetActiveGroups("GAZP@TQBR");
        Assert.Empty(noGroups);
    }

    [Fact]
    public void GetGroupById_ReturnsCorrectGroup()
    {
        var signal = CreateValidBuySignal(groupId: "my-group");

        _manager.CreateOrderGroup(signal);

        var group = _manager.GetGroupById("my-group");

        Assert.NotNull(group);
        Assert.Equal("my-group", group.GroupId);
    }

    [Fact]
    public void GetGroupById_ReturnsNullForUnknownId()
    {
        var group = _manager.GetGroupById("non-existent");

        Assert.Null(group);
    }

    [Fact]
    public void CreateOrderGroup_MaxGroupsExceeded_Throws()
    {
        var limits = new OrderGroupLimits(maxGroupsPerSecurity: 2);
        var manager = new OrderGroupManager(_operations, limits);

        manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group1"));
        manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group2"));

        Assert.Throws<InvalidOperationException>(() =>
            manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group3")));
    }

    [Fact]
    public void CalculateRiskPercent_CalculatesCorrectly()
    {
        // Risk = (Entry Price × Volume × Stop Distance %) / Current Equity
        // Entry: 100, Volume: 100, Stop: 95, Equity: 10000
        // Stop Distance % = (100 - 95) / 100 = 5%
        // Risk = (100 × 100 × 0.05) / 10000 = 500 / 10000 = 5%

        var risk = _manager.CalculateRiskPercent(
            entryPrice: 100m,
            volume: 100m,
            stopLossPrice: 95m,
            currentEquity: 10000m);

        Assert.Equal(5m, risk);
    }

    [Fact]
    public void CalculateRiskPercent_ForSellPosition_CalculatesCorrectly()
    {
        // Entry: 100, Volume: 100, Stop: 105, Equity: 10000
        // Stop Distance % = (105 - 100) / 100 = 5%
        // Risk = (100 × 100 × 0.05) / 10000 = 500 / 10000 = 5%

        var risk = _manager.CalculateRiskPercent(
            entryPrice: 100m,
            volume: 100m,
            stopLossPrice: 105m,
            currentEquity: 10000m);

        Assert.Equal(5m, risk);
    }

    [Fact]
    public void CreateOrderGroup_MultipleGroups_TrackedIndependently()
    {
        var signal1 = CreateValidBuySignal(entryPrice: 100m, groupId: "group1");
        var signal2 = CreateValidBuySignal(entryPrice: 105m, groupId: "group2");
        var signal3 = CreateValidSellSignal(entryPrice: 110m, groupId: "group3");

        var group1 = _manager.CreateOrderGroup(signal1);
        var group2 = _manager.CreateOrderGroup(signal2);
        var group3 = _manager.CreateOrderGroup(signal3);

        Assert.Equal(3, _manager.GetActiveGroups().Count);
        Assert.Equal(100m, group1.OpeningOrder.Price);
        Assert.Equal(105m, group2.OpeningOrder.Price);
        Assert.Equal(110m, group3.OpeningOrder.Price);
        Assert.Equal(Sides.Buy, group1.Direction);
        Assert.Equal(Sides.Buy, group2.Direction);
        Assert.Equal(Sides.Sell, group3.Direction);
    }

    [Fact]
    public void CreateOrderGroup_MultipleGroups_EachHasOwnState()
    {
        var signal1 = CreateValidBuySignal(groupId: "group1");
        var signal2 = CreateValidBuySignal(groupId: "group2");

        var group1 = _manager.CreateOrderGroup(signal1);
        var group2 = _manager.CreateOrderGroup(signal2);

        var trade = CreateMyTrade(group1.OpeningOrder.BrokerOrder!, 100m, 100m);
        _manager.OnOrderFilled(group1.OpeningOrder.BrokerOrder!, trade);

        Assert.Equal(OrderGroupState.Active, group1.State);
        Assert.Equal(OrderGroupState.Pending, group2.State);
    }

    [Fact]
    public void CreateOrderGroup_MaxRiskPercentExceeded_Throws()
    {
        var limits = new OrderGroupLimits(maxRiskPercentPerGroup: 2m);
        var manager = new OrderGroupManager(_operations, limits);

        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        Assert.Throws<InvalidOperationException>(() =>
            manager.CreateOrderGroup(signal, currentEquity: 10000m));
    }

    [Fact]
    public void CreateOrderGroup_RiskWithinLimit_Succeeds()
    {
        var limits = new OrderGroupLimits(maxRiskPercentPerGroup: 10m);
        var manager = new OrderGroupManager(_operations, limits);

        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = manager.CreateOrderGroup(signal, currentEquity: 10000m);

        Assert.NotNull(group);
    }

    [Fact]
    public void CreateOrderGroup_NoEquityProvided_SkipsRiskValidation()
    {
        var limits = new OrderGroupLimits(maxRiskPercentPerGroup: 1m);
        var manager = new OrderGroupManager(_operations, limits);

        var signal = CreateValidBuySignal();

        var group = manager.CreateOrderGroup(signal);

        Assert.NotNull(group);
    }

    [Fact]
    public void CreateOrderGroup_NoStopLoss_SkipsRiskValidation()
    {
        var limits = new OrderGroupLimits(maxRiskPercentPerGroup: 1m);
        var manager = new OrderGroupManager(_operations, limits);

        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders);

        var group = manager.CreateOrderGroup(signal, currentEquity: 10000m);

        Assert.NotNull(group);
    }

    #endregion

    #region User Story 1 Additional Tests: Limits Property

    [Fact]
    public void Limits_ReturnsConfiguredLimits()
    {
        Assert.Same(_limits, _manager.Limits);
    }

    [Fact]
    public void Constructor_WithNullOperations_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderGroupManager(null!, _limits));
    }

    [Fact]
    public void Constructor_WithNullLimits_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderGroupManager(_operations, null!));
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllGroups()
    {
        _manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group1"));
        _manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group2"));

        _manager.Reset();

        Assert.Empty(_manager.GetActiveGroups());
    }

    #endregion

    #region User Story 2: Opening Order Activation Triggers Closing Orders

    private static MyTrade CreateMyTrade(Order order, decimal volume, decimal price)
    {
        return new MyTrade
        {
            Order = order,
            Trade = new ExecutionMessage { TradeVolume = volume, TradePrice = price }
        };
    }

    [Fact]
    public void OnOrderFilled_FullFill_PlacesAllClosingOrders()
    {
        var signal = CreateValidBuySignal(volume: 100m);
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        Assert.Equal(OrderGroupState.Active, group.State);
        Assert.Equal(3, _operations.Orders.Count);

        Assert.All(group.ClosingOrders, o =>
        {
            Assert.NotNull(o.BrokerOrder);
            Assert.Equal(GroupedOrderState.Active, o.State);
        });
    }

    [Fact]
    public void OnOrderFilled_PartialFill_ScalesClosingOrdersProportionally()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 50m),
            new(120m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 60m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        Assert.Equal(OrderGroupState.Active, group.State);
        Assert.Equal(3, _operations.Orders.Count);

        var closingBrokerOrders = _operations.Orders.Skip(1).ToList();
        Assert.Equal(30m, closingBrokerOrders[0].Volume);
        Assert.Equal(30m, closingBrokerOrders[1].Volume);
    }

    [Fact]
    public void OnOrderFilled_PlacesClosingOrdersWithOppositeDirection()
    {
        var signal = CreateValidBuySignal(volume: 100m);
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        var closingBrokerOrders = _operations.Orders.Skip(1).ToList();
        Assert.All(closingBrokerOrders, o => Assert.Equal(Sides.Sell, o.Side));
    }

    [Fact]
    public void OnOrderFilled_FiresOrderActivatedEvent()
    {
        var signal = CreateValidBuySignal();
        var group = _manager.CreateOrderGroup(signal);

        OrderGroup? activatedGroup = null;
        GroupedOrder? activatedOrder = null;

        _manager.OrderActivated += (g, o) =>
        {
            activatedGroup = g;
            activatedOrder = o;
        };

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        Assert.Same(group, activatedGroup);
        Assert.Same(group.OpeningOrder, activatedOrder);
    }

    [Fact]
    public void OnOrderFilled_OpensOrderMarkedAsFilled()
    {
        var signal = CreateValidBuySignal(volume: 100m);
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        Assert.Equal(GroupedOrderState.Filled, group.OpeningOrder.State);
        Assert.Equal(100m, group.OpeningOrder.FilledVolume);
    }

    [Fact]
    public void OnOrderFilled_SellPosition_PlacesBuyClosingOrders()
    {
        var signal = CreateValidSellSignal(volume: 100m);
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);

        _manager.OnOrderFilled(openingBrokerOrder, trade);

        var closingBrokerOrders = _operations.Orders.Skip(1).ToList();
        Assert.All(closingBrokerOrders, o => Assert.Equal(Sides.Buy, o.Side));
    }

    #endregion

    #region User Story 4: Close Order Group with Position Unwinding

    [Fact]
    public void CloseGroup_WhenOpeningNotFilled_CancelsOpeningOrder()
    {
        var signal = CreateValidBuySignal(groupId: "group-to-close");
        var group = _manager.CreateOrderGroup(signal);

        _manager.CloseGroup("group-to-close");

        Assert.Equal(OrderGroupState.Closing, group.State);
        Assert.Equal(GroupedOrderState.Cancelled, group.OpeningOrder.State);
    }

    [Fact]
    public void CloseGroup_WhenOpeningFilled_PlacesMarketCloseOrder()
    {
        var signal = CreateValidBuySignal(volume: 100m, groupId: "group-to-close");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        _operations.ClearOrders();

        _manager.CloseGroup("group-to-close");

        Assert.Equal(OrderGroupState.Closing, group.State);
        Assert.Single(_operations.Orders);
        var marketOrder = _operations.Orders[0];
        Assert.Equal(Sides.Sell, marketOrder.Side);
        Assert.Equal(OrderTypes.Market, marketOrder.Type);
    }

    [Fact]
    public void CloseGroup_CancelsAllPendingClosingOrders()
    {
        var signal = CreateValidBuySignal(volume: 100m, groupId: "group-to-close");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        _manager.CloseGroup("group-to-close");

        Assert.All(group.ClosingOrders, o => Assert.Equal(GroupedOrderState.Cancelled, o.State));
    }

    [Fact]
    public void CloseGroup_WithPartiallyFilledClosingOrders_CalculatesCorrectRemainingVolume()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 50m),
            new(120m, 50m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m,
            groupId: "group-partial");

        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var openTrade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, openTrade);

        var closingBrokerOrder1 = group.ClosingOrders[0].BrokerOrder!;
        var closeTrade = CreateMyTrade(closingBrokerOrder1, 30m, 110m);
        _manager.OnOrderFilled(closingBrokerOrder1, closeTrade);

        _operations.ClearOrders();

        _manager.CloseGroup("group-partial");

        Assert.Single(_operations.Orders);
        var marketOrder = _operations.Orders[0];
        Assert.Equal(70m, marketOrder.Volume);
    }

    [Fact]
    public void CloseAllGroups_ClosesAllActiveGroups()
    {
        _manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group1"));
        _manager.CreateOrderGroup(CreateValidBuySignal(groupId: "group2"));

        _manager.CloseAllGroups();

        var groups = _manager.GetActiveGroups();
        Assert.All(groups, g => Assert.Equal(OrderGroupState.Closing, g.State));
    }

    [Fact]
    public void CloseGroup_FiresGroupCancelledEvent()
    {
        var signal = CreateValidBuySignal(groupId: "group-to-close");
        var group = _manager.CreateOrderGroup(signal);

        OrderGroup? cancelledGroup = null;
        _manager.GroupCancelled += g => cancelledGroup = g;

        _manager.CloseGroup("group-to-close");

        Assert.Same(group, cancelledGroup);
    }

    [Fact]
    public void CloseGroup_UnknownGroupId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _manager.CloseGroup("non-existent-group"));
    }

    #endregion

    #region User Story 5: Adjust Single Order Activation Price

    [Fact]
    public void AdjustOrderPrice_OnPendingOpeningOrder_UpdatesPrice()
    {
        var signal = CreateValidBuySignal(entryPrice: 100m, groupId: "adjust-test");
        var group = _manager.CreateOrderGroup(signal);
        var openingOrderId = group.OpeningOrder.OrderId;

        _manager.AdjustOrderPrice("adjust-test", openingOrderId, 99m);

        Assert.Equal(99m, group.OpeningOrder.Price);
    }

    [Fact]
    public void AdjustOrderPrice_CancelsOldOrderAndPlacesNew()
    {
        var signal = CreateValidBuySignal(entryPrice: 100m, groupId: "adjust-test");
        var group = _manager.CreateOrderGroup(signal);
        var openingOrderId = group.OpeningOrder.OrderId;
        var originalBrokerOrder = group.OpeningOrder.BrokerOrder;

        _manager.AdjustOrderPrice("adjust-test", openingOrderId, 99m);

        Assert.Equal(OrderStates.Done, originalBrokerOrder!.State);
        Assert.NotSame(originalBrokerOrder, group.OpeningOrder.BrokerOrder);
        Assert.Equal(99m, group.OpeningOrder.BrokerOrder!.Price);
    }

    [Fact]
    public void AdjustOrderPrice_OnFilledOrder_Throws()
    {
        var signal = CreateValidBuySignal(volume: 100m, groupId: "adjust-test");
        var group = _manager.CreateOrderGroup(signal);
        var openingOrderId = group.OpeningOrder.OrderId;

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        Assert.Throws<InvalidOperationException>(() =>
            _manager.AdjustOrderPrice("adjust-test", openingOrderId, 99m));
    }

    [Fact]
    public void AdjustOrderPrice_OnActiveClosingOrder_UpdatesPrice()
    {
        var signal = CreateValidBuySignal(volume: 100m, groupId: "adjust-test");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        var closingOrderId = group.ClosingOrders[0].OrderId;

        _manager.AdjustOrderPrice("adjust-test", closingOrderId, 115m);

        Assert.Equal(115m, group.ClosingOrders[0].Price);
    }

    [Fact]
    public void AdjustOrderPrice_UnknownGroupId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _manager.AdjustOrderPrice("non-existent", "order-id", 100m));
    }

    [Fact]
    public void AdjustOrderPrice_UnknownOrderId_Throws()
    {
        var signal = CreateValidBuySignal(groupId: "adjust-test");
        _manager.CreateOrderGroup(signal);

        Assert.Throws<InvalidOperationException>(() =>
            _manager.AdjustOrderPrice("adjust-test", "non-existent-order", 100m));
    }

    #endregion

    #region User Story 6: Market Closing Orders

    [Fact]
    public void CreateOrderGroup_WithMarketClosingOrders_CreatesCorrectly()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(0m, 50m, OrderTypes.Market),
            new(110m, 50m, OrderTypes.Limit)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = _manager.CreateOrderGroup(signal);

        Assert.Equal(OrderTypes.Market, group.ClosingOrders[0].OrderType);
        Assert.Equal(OrderTypes.Limit, group.ClosingOrders[1].OrderType);
    }

    [Fact]
    public void OnOrderFilled_PlacesMarketClosingOrdersCorrectly()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(0m, 50m, OrderTypes.Market),
            new(110m, 50m, OrderTypes.Limit)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        var closingBrokerOrders = _operations.Orders.Skip(1).ToList();
        Assert.Equal(OrderTypes.Market, closingBrokerOrders[0].Type);
        Assert.Equal(OrderTypes.Limit, closingBrokerOrders[1].Type);
    }

    [Fact]
    public void OnOrderFilled_MixedMarketAndLimitClosingOrders_AllPlacedCorrectly()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(0m, 30m, OrderTypes.Market),
            new(110m, 40m, OrderTypes.Limit),
            new(0m, 30m, OrderTypes.Market)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m);

        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var trade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, trade);

        var closingBrokerOrders = _operations.Orders.Skip(1).ToList();
        Assert.Equal(3, closingBrokerOrders.Count);
        Assert.Equal(OrderTypes.Market, closingBrokerOrders[0].Type);
        Assert.Equal(OrderTypes.Limit, closingBrokerOrders[1].Type);
        Assert.Equal(OrderTypes.Market, closingBrokerOrders[2].Type);
    }

    #endregion

    #region Phase 10: Edge Cases and Event Handling

    [Fact]
    public void OnOrderCancelled_MarksOrderAsCancelled()
    {
        var signal = CreateValidBuySignal(groupId: "cancel-test");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        _manager.OnOrderCancelled(openingBrokerOrder);

        Assert.Equal(GroupedOrderState.Cancelled, group.OpeningOrder.State);
    }

    [Fact]
    public void OnOrderCancelled_OpeningOrder_CancelsGroup()
    {
        var signal = CreateValidBuySignal(groupId: "cancel-test");
        var group = _manager.CreateOrderGroup(signal);

        OrderGroup? cancelledGroup = null;
        _manager.GroupCancelled += g => cancelledGroup = g;

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        _manager.OnOrderCancelled(openingBrokerOrder);

        Assert.Equal(OrderGroupState.Cancelled, group.State);
        Assert.Same(group, cancelledGroup);
    }

    [Fact]
    public void OnOrderRejected_MarksOrderAsRejected()
    {
        var signal = CreateValidBuySignal(groupId: "reject-test");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        _manager.OnOrderRejected(openingBrokerOrder);

        Assert.Equal(GroupedOrderState.Rejected, group.OpeningOrder.State);
    }

    [Fact]
    public void OnOrderRejected_FiresOrderRejectedEvent()
    {
        var signal = CreateValidBuySignal(groupId: "reject-test");
        var group = _manager.CreateOrderGroup(signal);

        OrderGroup? rejectedGroup = null;
        GroupedOrder? rejectedOrder = null;
        _manager.OrderRejected += (g, o) =>
        {
            rejectedGroup = g;
            rejectedOrder = o;
        };

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        _manager.OnOrderRejected(openingBrokerOrder);

        Assert.Same(group, rejectedGroup);
        Assert.Same(group.OpeningOrder, rejectedOrder);
    }

    [Fact]
    public void OnOrderRejected_OpeningOrder_CancelsGroup()
    {
        var signal = CreateValidBuySignal(groupId: "reject-test");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        _manager.OnOrderRejected(openingBrokerOrder);

        Assert.Equal(OrderGroupState.Cancelled, group.State);
    }

    [Fact]
    public void OnOrderFilled_PartialClosingFill_UpdatesState()
    {
        var signal = CreateValidBuySignal(volume: 100m, groupId: "partial-close-test");
        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var openTrade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, openTrade);

        var closingBrokerOrder = group.ClosingOrders[0].BrokerOrder!;
        var closeTrade = CreateMyTrade(closingBrokerOrder, 25m, 110m);
        _manager.OnOrderFilled(closingBrokerOrder, closeTrade);

        Assert.Equal(GroupedOrderState.PartiallyFilled, group.ClosingOrders[0].State);
        Assert.Equal(25m, group.ClosingOrders[0].FilledVolume);
    }

    [Fact]
    public void OnOrderFilled_AllClosingOrdersFilled_CompletesGroup()
    {
        var closingOrders = new List<ClosingOrderDefinition>
        {
            new(110m, 100m)
        };

        var signal = new ExtendedTradeSignal(
            direction: Sides.Buy,
            entryPrice: 100m,
            entryVolume: 100m,
            closingOrders: closingOrders,
            stopLossPrice: 95m,
            groupId: "complete-test");

        var group = _manager.CreateOrderGroup(signal);

        var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
        var openTrade = CreateMyTrade(openingBrokerOrder, 100m, 100m);
        _manager.OnOrderFilled(openingBrokerOrder, openTrade);

        OrderGroup? completedGroup = null;
        _manager.GroupCompleted += g => completedGroup = g;

        var closingBrokerOrder = group.ClosingOrders[0].BrokerOrder!;
        var closeTrade = CreateMyTrade(closingBrokerOrder, 100m, 110m);
        _manager.OnOrderFilled(closingBrokerOrder, closeTrade);

        Assert.Equal(OrderGroupState.Completed, group.State);
        Assert.Same(group, completedGroup);
    }

    [Fact]
    public void OnOrderFilled_UnknownOrder_IsIgnored()
    {
        var unknownOrder = new Order { Id = 99999 };
        var trade = CreateMyTrade(unknownOrder, 100m, 100m);

        _manager.OnOrderFilled(unknownOrder, trade);
    }

    [Fact]
    public void OnOrderCancelled_UnknownOrder_IsIgnored()
    {
        var unknownOrder = new Order { Id = 99999 };

        _manager.OnOrderCancelled(unknownOrder);
    }

    [Fact]
    public void OnOrderRejected_UnknownOrder_IsIgnored()
    {
        var unknownOrder = new Order { Id = 99999 };

        _manager.OnOrderRejected(unknownOrder);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void Performance_Create100SimultaneousOrderGroups_CompletesQuickly()
    {
        var limits = new OrderGroupLimits(maxGroupsPerSecurity: 100);
        var manager = new OrderGroupManager(_operations, limits);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (var i = 0; i < 100; i++)
        {
            var closingOrders = new List<ClosingOrderDefinition>
            {
                new(110m + i, 30m),
                new(115m + i, 30m),
                new(120m + i, 40m)
            };

            var signal = new ExtendedTradeSignal(
                direction: Sides.Buy,
                entryPrice: 100m + i,
                entryVolume: 100m,
                closingOrders: closingOrders,
                stopLossPrice: 95m,
                groupId: $"perf-test-{i}");

            manager.CreateOrderGroup(signal);
        }

        stopwatch.Stop();

        var activeGroups = manager.GetActiveGroups();
        Assert.Equal(100, activeGroups.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Creating 100 groups took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public void Performance_Process100OrderFills_CompletesQuickly()
    {
        var limits = new OrderGroupLimits(maxGroupsPerSecurity: 100);
        var manager = new OrderGroupManager(_operations, limits);

        var groups = new List<OrderGroup>();
        for (var i = 0; i < 100; i++)
        {
            var closingOrders = new List<ClosingOrderDefinition>
            {
                new(110m + i, 50m),
                new(120m + i, 50m)
            };

            var signal = new ExtendedTradeSignal(
                direction: Sides.Buy,
                entryPrice: 100m + i,
                entryVolume: 100m,
                closingOrders: closingOrders,
                stopLossPrice: 95m,
                groupId: $"fill-test-{i}");

            groups.Add(manager.CreateOrderGroup(signal));
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var group in groups)
        {
            var openingBrokerOrder = group.OpeningOrder.BrokerOrder!;
            var trade = CreateMyTrade(openingBrokerOrder, 100m, openingBrokerOrder.Price);
            manager.OnOrderFilled(openingBrokerOrder, trade);
        }

        stopwatch.Stop();

        var activeGroups = manager.GetActiveGroups();
        Assert.True(activeGroups.All(g => g.State == OrderGroupState.Active));
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Processing 100 fills took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    #endregion
}
