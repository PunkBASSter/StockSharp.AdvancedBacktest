using StockSharp.AdvancedBacktest.Infrastructure.OrderManagement;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.OrderManagement;

public class OrderGroupJsonPersistenceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly OrderGroupJsonPersistence _persistence;

    public OrderGroupJsonPersistenceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OrderGroupTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _persistence = new OrderGroupJsonPersistence(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static OrderGroup CreateTestOrderGroup(
        string groupId = "test-group",
        string securityId = "SBER@TQBR",
        Sides direction = Sides.Buy)
    {
        var openingOrder = new GroupedOrder(
            orderId: "open_1",
            role: GroupedOrderRole.Opening,
            price: 100m,
            volume: 100m,
            orderType: OrderTypes.Limit);

        var closingOrders = new List<GroupedOrder>
        {
            new(
                orderId: "close_1",
                role: GroupedOrderRole.Closing,
                price: 110m,
                volume: 50m,
                orderType: OrderTypes.Limit),
            new(
                orderId: "close_2",
                role: GroupedOrderRole.Closing,
                price: 120m,
                volume: 50m,
                orderType: OrderTypes.Limit)
        };

        return new OrderGroup(
            groupId: groupId,
            securityId: securityId,
            direction: direction,
            openingOrder: openingOrder,
            closingOrders: closingOrders);
    }

    #region Constructor and IsEnabled

    [Fact]
    public void Constructor_CreatesDirectoryIfNotExists()
    {
        var newDir = Path.Combine(_testDirectory, "new_subdir");

        var persistence = new OrderGroupJsonPersistence(newDir);

        Assert.True(Directory.Exists(newDir));
    }

    [Fact]
    public void IsEnabled_ReturnsTrue()
    {
        Assert.True(_persistence.IsEnabled);
    }

    #endregion

    #region Save

    [Fact]
    public void Save_WritesJsonFile()
    {
        var group = CreateTestOrderGroup();
        var groups = new List<OrderGroup> { group };

        _persistence.Save("SBER@TQBR", groups);

        var expectedPath = Path.Combine(_testDirectory, "SBER_TQBR.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void Save_MultipleGroups_WritesAllGroups()
    {
        var group1 = CreateTestOrderGroup(groupId: "group1");
        var group2 = CreateTestOrderGroup(groupId: "group2");
        var groups = new List<OrderGroup> { group1, group2 };

        _persistence.Save("SBER@TQBR", groups);

        var loaded = _persistence.Load("SBER@TQBR");
        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var group1 = CreateTestOrderGroup(groupId: "original");
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group1 });

        var group2 = CreateTestOrderGroup(groupId: "updated");
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group2 });

        var loaded = _persistence.Load("SBER@TQBR");
        Assert.Single(loaded);
        Assert.Equal("updated", loaded[0].GroupId);
    }

    [Fact]
    public void Save_SanitizesSecurityIdForFilename()
    {
        var group = CreateTestOrderGroup(securityId: "SBER@TQBR");
        var groups = new List<OrderGroup> { group };

        _persistence.Save("SBER@TQBR", groups);

        var expectedPath = Path.Combine(_testDirectory, "SBER_TQBR.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public void Save_EmptyList_DeletesFile()
    {
        var group = CreateTestOrderGroup();
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        _persistence.Save("SBER@TQBR", new List<OrderGroup>());

        var expectedPath = Path.Combine(_testDirectory, "SBER_TQBR.json");
        Assert.False(File.Exists(expectedPath));
    }

    #endregion

    #region Load

    [Fact]
    public void Load_ReturnsGroupsFromFile()
    {
        var group = CreateTestOrderGroup(groupId: "my-group");
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        var loaded = _persistence.Load("SBER@TQBR");

        Assert.Single(loaded);
        Assert.Equal("my-group", loaded[0].GroupId);
    }

    [Fact]
    public void Load_PreservesAllProperties()
    {
        var group = CreateTestOrderGroup(
            groupId: "preserve-test",
            securityId: "GAZP@TQBR",
            direction: Sides.Sell);

        group.MarkActivated();

        _persistence.Save("GAZP@TQBR", new List<OrderGroup> { group });

        var loaded = _persistence.Load("GAZP@TQBR");

        Assert.Single(loaded);
        var loadedGroup = loaded[0];
        Assert.Equal("preserve-test", loadedGroup.GroupId);
        Assert.Equal("GAZP@TQBR", loadedGroup.SecurityId);
        Assert.Equal(Sides.Sell, loadedGroup.Direction);
        Assert.Equal(OrderGroupState.Active, loadedGroup.State);
        Assert.NotNull(loadedGroup.ActivatedAt);
    }

    [Fact]
    public void Load_PreservesOpeningOrderProperties()
    {
        var group = CreateTestOrderGroup();
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        var loaded = _persistence.Load("SBER@TQBR");

        var openingOrder = loaded[0].OpeningOrder;
        Assert.Equal("open_1", openingOrder.OrderId);
        Assert.Equal(GroupedOrderRole.Opening, openingOrder.Role);
        Assert.Equal(100m, openingOrder.Price);
        Assert.Equal(100m, openingOrder.Volume);
        Assert.Equal(OrderTypes.Limit, openingOrder.OrderType);
    }

    [Fact]
    public void Load_PreservesClosingOrdersProperties()
    {
        var group = CreateTestOrderGroup();
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        var loaded = _persistence.Load("SBER@TQBR");

        Assert.Equal(2, loaded[0].ClosingOrders.Count);
        Assert.Equal("close_1", loaded[0].ClosingOrders[0].OrderId);
        Assert.Equal(110m, loaded[0].ClosingOrders[0].Price);
        Assert.Equal(50m, loaded[0].ClosingOrders[0].Volume);
        Assert.Equal("close_2", loaded[0].ClosingOrders[1].OrderId);
        Assert.Equal(120m, loaded[0].ClosingOrders[1].Price);
        Assert.Equal(50m, loaded[0].ClosingOrders[1].Volume);
    }

    [Fact]
    public void Load_ReturnsEmptyListWhenFileNotExists()
    {
        var loaded = _persistence.Load("NON_EXISTENT@TQBR");

        Assert.Empty(loaded);
    }

    #endregion

    #region LoadAll

    [Fact]
    public void LoadAll_LoadsAllSecurities()
    {
        var sberGroup = CreateTestOrderGroup(groupId: "sber-group", securityId: "SBER@TQBR");
        var gazpGroup = CreateTestOrderGroup(groupId: "gazp-group", securityId: "GAZP@TQBR");

        _persistence.Save("SBER@TQBR", new List<OrderGroup> { sberGroup });
        _persistence.Save("GAZP@TQBR", new List<OrderGroup> { gazpGroup });

        var allGroups = _persistence.LoadAll();

        Assert.Equal(2, allGroups.Count);
        Assert.True(allGroups.ContainsKey("SBER@TQBR"));
        Assert.True(allGroups.ContainsKey("GAZP@TQBR"));
        Assert.Contains(allGroups["SBER@TQBR"], g => g.GroupId == "sber-group");
        Assert.Contains(allGroups["GAZP@TQBR"], g => g.GroupId == "gazp-group");
    }

    [Fact]
    public void LoadAll_ReturnsEmptyWhenNoFiles()
    {
        var emptyDir = Path.Combine(_testDirectory, "empty_subdir");
        Directory.CreateDirectory(emptyDir);
        var persistence = new OrderGroupJsonPersistence(emptyDir);

        var allGroups = persistence.LoadAll();

        Assert.Empty(allGroups);
    }

    [Fact]
    public void LoadAll_IgnoresNonJsonFiles()
    {
        var group = CreateTestOrderGroup();
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        File.WriteAllText(Path.Combine(_testDirectory, "readme.txt"), "test");

        var allGroups = _persistence.LoadAll();

        Assert.Single(allGroups);
        Assert.True(allGroups.ContainsKey("SBER@TQBR"));
    }

    #endregion

    #region Delete

    [Fact]
    public void Delete_RemovesFile()
    {
        var group = CreateTestOrderGroup();
        _persistence.Save("SBER@TQBR", new List<OrderGroup> { group });

        _persistence.Delete("SBER@TQBR");

        var expectedPath = Path.Combine(_testDirectory, "SBER_TQBR.json");
        Assert.False(File.Exists(expectedPath));
    }

    [Fact]
    public void Delete_DoesNothingWhenFileNotExists()
    {
        _persistence.Delete("NON_EXISTENT@TQBR");
    }

    #endregion

    #region NullOrderGroupPersistence

    [Fact]
    public void NullOrderGroupPersistence_IsEnabled_ReturnsFalse()
    {
        var nullPersistence = NullOrderGroupPersistence.Instance;

        Assert.False(nullPersistence.IsEnabled);
    }

    [Fact]
    public void NullOrderGroupPersistence_Save_DoesNothing()
    {
        var nullPersistence = NullOrderGroupPersistence.Instance;
        var group = CreateTestOrderGroup();

        nullPersistence.Save("SBER@TQBR", new List<OrderGroup> { group });
    }

    [Fact]
    public void NullOrderGroupPersistence_Load_ReturnsEmpty()
    {
        var nullPersistence = NullOrderGroupPersistence.Instance;

        var loaded = nullPersistence.Load("SBER@TQBR");

        Assert.Empty(loaded);
    }

    [Fact]
    public void NullOrderGroupPersistence_LoadAll_ReturnsEmpty()
    {
        var nullPersistence = NullOrderGroupPersistence.Instance;

        var allGroups = nullPersistence.LoadAll();

        Assert.Empty(allGroups);
    }

    [Fact]
    public void NullOrderGroupPersistence_Delete_DoesNothing()
    {
        var nullPersistence = NullOrderGroupPersistence.Instance;

        nullPersistence.Delete("SBER@TQBR");
    }

    #endregion
}
