using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Xunit;

namespace StockSharp.AdvancedBacktest.Core.Tests.OrderManagement;

public class EntryOrderGroupMatchesTests
{
    [Fact]
    public void Matches_SameEntryPriceAndSideAndVolume_ReturnsTrue()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 110m, null)]);

        Assert.True(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentEntryPrice_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(101m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 110m, null)]);

        Assert.False(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentSide_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Sell, 1m),
            [new ProtectivePair(95m, 110m, null)]);

        Assert.False(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentVolume_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 2m),
            [new ProtectivePair(95m, 110m, null)]);

        Assert.False(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentStopLossPrice_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(94m, 110m, null)]);

        Assert.False(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentTakeProfitPrice_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 115m, null)]);

        Assert.False(group.Matches(request));
    }

    [Fact]
    public void Matches_WithinTolerance_ReturnsTrue()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100.000000001m, Sides.Buy, 1m),
            [new ProtectivePair(95.000000001m, 110.000000001m, null)]);

        Assert.True(group.Matches(request, tolerance: 0.00001m));
    }

    [Fact]
    public void Matches_MultiplePairs_MatchesAll()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.5m),
            new(95m, 110m, 0.5m)
        };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 105m, 0.5m), new ProtectivePair(95m, 110m, 0.5m)]);

        Assert.True(group.Matches(request));
    }

    [Fact]
    public void Matches_DifferentPairCount_ReturnsFalse()
    {
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var registry = new OrderRegistry("test");
        var group = registry.RegisterGroup(entry, pairs);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 105m, 0.5m), new ProtectivePair(95m, 110m, 0.5m)]);

        Assert.False(group.Matches(request));
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRegistryRegisterGroupTests
{
    [Fact]
    public void RegisterGroup_WithValidOrder_CreatesGroup()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

        var group = registry.RegisterGroup(entry, pairs);

        Assert.NotNull(group);
        Assert.Equal(OrderGroupState.Pending, group.State);
        Assert.Equal(entry, group.EntryOrder);
        Assert.NotEmpty(group.GroupId);
    }

    [Fact]
    public void RegisterGroup_WithNullOrder_ThrowsArgumentNullException()
    {
        var registry = new OrderRegistry("test");
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };

        Assert.Throws<ArgumentNullException>(() => registry.RegisterGroup(null!, pairs));
    }

    [Fact]
    public void RegisterGroup_MultiplePairs_VolumesMustSumToEntryVolume()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var invalidPairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.3m),
            new(95m, 110m, 0.3m)  // Sum = 0.6, should be 1.0
        };

        Assert.Throws<ArgumentException>(() => registry.RegisterGroup(entry, invalidPairs));
    }

    [Fact]
    public void RegisterGroup_MultiplePairs_ValidVolumeSum_Succeeds()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var validPairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.5m),
            new(95m, 110m, 0.5m)  // Sum = 1.0, equals entry volume
        };

        var group = registry.RegisterGroup(entry, validPairs);

        Assert.NotNull(group);
        Assert.Equal(2, group.ProtectivePairs.Count);
    }

    [Fact]
    public void RegisterGroup_StoresProtectivePairs()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair>
        {
            new(95m, 105m, 0.5m),
            new(95m, 110m, 0.5m)
        };

        var group = registry.RegisterGroup(entry, pairs);

        Assert.Equal(2, group.ProtectivePairs.Count);
        var specs = group.ProtectivePairs.Values.Select(pp => pp.Spec).ToList();
        Assert.Contains(specs, s => s.TakeProfitPrice == 105m);
        Assert.Contains(specs, s => s.TakeProfitPrice == 110m);
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRegistryGetActiveGroupsTests
{
    [Fact]
    public void GetActiveGroups_NoGroups_ReturnsEmpty()
    {
        var registry = new OrderRegistry("test");

        var active = registry.GetActiveGroups();

        Assert.Empty(active);
    }

    [Fact]
    public void GetActiveGroups_WithPendingGroup_ReturnsGroup()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var group = registry.RegisterGroup(entry, pairs);

        var active = registry.GetActiveGroups();

        Assert.Single(active);
        Assert.Contains(group, active);
    }

    [Fact]
    public void GetActiveGroups_WithClosedGroup_ExcludesIt()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var pairs = new List<ProtectivePair> { new(95m, 110m, null) };
        var group = registry.RegisterGroup(entry, pairs);
        group.State = OrderGroupState.Closed;

        var active = registry.GetActiveGroups();

        Assert.Empty(active);
    }

    [Fact]
    public void GetActiveGroups_MixedStates_ReturnsOnlyActive()
    {
        var registry = new OrderRegistry("test");

        var entry1 = CreateOrder(100m, Sides.Buy, 1m);
        var group1 = registry.RegisterGroup(entry1, [new ProtectivePair(95m, 110m, null)]);
        group1.State = OrderGroupState.Closed;

        var entry2 = CreateOrder(101m, Sides.Buy, 1m);
        var group2 = registry.RegisterGroup(entry2, [new ProtectivePair(96m, 111m, null)]);

        var entry3 = CreateOrder(102m, Sides.Buy, 1m);
        var group3 = registry.RegisterGroup(entry3, [new ProtectivePair(97m, 112m, null)]);
        group3.State = OrderGroupState.ProtectionActive;

        var active = registry.GetActiveGroups();

        Assert.Equal(2, active.Length);
        Assert.Contains(group2, active);
        Assert.Contains(group3, active);
        Assert.DoesNotContain(group1, active);
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRegistryFindMatchingGroupTests
{
    [Fact]
    public void FindMatchingGroup_NoMatches_ReturnsNull()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        registry.RegisterGroup(entry, [new ProtectivePair(95m, 110m, null)]);

        var request = new OrderRequest(
            CreateOrder(105m, Sides.Buy, 1m),
            [new ProtectivePair(100m, 115m, null)]);

        var match = registry.FindMatchingGroup(request);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatchingGroup_ExactMatch_ReturnsGroup()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var group = registry.RegisterGroup(entry, [new ProtectivePair(95m, 110m, null)]);

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 110m, null)]);

        var match = registry.FindMatchingGroup(request);

        Assert.Equal(group, match);
    }

    [Fact]
    public void FindMatchingGroup_ClosedGroup_NotReturned()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var group = registry.RegisterGroup(entry, [new ProtectivePair(95m, 110m, null)]);
        group.State = OrderGroupState.Closed;

        var request = new OrderRequest(
            CreateOrder(100m, Sides.Buy, 1m),
            [new ProtectivePair(95m, 110m, null)]);

        var match = registry.FindMatchingGroup(request);

        Assert.Null(match);
    }

    [Fact]
    public void FindMatchingGroup_WithTolerance_MatchesCloseValues()
    {
        var registry = new OrderRegistry("test");
        var entry = CreateOrder(100m, Sides.Buy, 1m);
        var group = registry.RegisterGroup(entry, [new ProtectivePair(95m, 110m, null)]);

        var request = new OrderRequest(
            CreateOrder(100.0001m, Sides.Buy, 1m),
            [new ProtectivePair(95.0001m, 110.0001m, null)]);

        var match = registry.FindMatchingGroup(request, tolerance: 0.001m);

        Assert.Equal(group, match);
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRegistryConcurrentLimitTests
{
    [Fact]
    public void MaxConcurrentGroups_DefaultValue_IsFive()
    {
        var registry = new OrderRegistry("test");

        Assert.Equal(5, registry.MaxConcurrentGroups);
    }

    [Fact]
    public void RegisterGroup_AtLimit_ThrowsInvalidOperationException()
    {
        var registry = new OrderRegistry("test") { MaxConcurrentGroups = 2 };

        registry.RegisterGroup(CreateOrder(100m, Sides.Buy, 1m), [new ProtectivePair(95m, 110m, null)]);
        registry.RegisterGroup(CreateOrder(101m, Sides.Buy, 1m), [new ProtectivePair(96m, 111m, null)]);

        Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterGroup(CreateOrder(102m, Sides.Buy, 1m), [new ProtectivePair(97m, 112m, null)]));
    }

    [Fact]
    public void RegisterGroup_AfterClosingOne_Succeeds()
    {
        var registry = new OrderRegistry("test") { MaxConcurrentGroups = 2 };

        var group1 = registry.RegisterGroup(CreateOrder(100m, Sides.Buy, 1m), [new ProtectivePair(95m, 110m, null)]);
        registry.RegisterGroup(CreateOrder(101m, Sides.Buy, 1m), [new ProtectivePair(96m, 111m, null)]);

        group1.State = OrderGroupState.Closed;

        var group3 = registry.RegisterGroup(CreateOrder(102m, Sides.Buy, 1m), [new ProtectivePair(97m, 112m, null)]);

        Assert.NotNull(group3);
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}

public class OrderRegistryResetTests
{
    [Fact]
    public void Reset_ClearsAllGroups()
    {
        var registry = new OrderRegistry("test");
        registry.RegisterGroup(CreateOrder(100m, Sides.Buy, 1m), [new ProtectivePair(95m, 110m, null)]);
        registry.RegisterGroup(CreateOrder(101m, Sides.Buy, 1m), [new ProtectivePair(96m, 111m, null)]);

        registry.Reset();

        Assert.Empty(registry.GetActiveGroups());
    }

    private static Order CreateOrder(decimal price, Sides side, decimal volume) =>
        new() { Price = price, Side = side, Volume = volume, Type = OrderTypes.Limit };
}
