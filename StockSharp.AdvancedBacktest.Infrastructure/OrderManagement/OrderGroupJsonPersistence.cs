using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.OrderManagement;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Infrastructure.OrderManagement;

public sealed class OrderGroupJsonPersistence : IOrderGroupPersistence
{
    private readonly string _directory;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public bool IsEnabled => true;

    public OrderGroupJsonPersistence(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        _directory = directory;

        if (!Directory.Exists(_directory))
        {
            Directory.CreateDirectory(_directory);
        }
    }

    public void Save(string securityId, IReadOnlyList<OrderGroup> groups)
    {
        var filePath = GetFilePath(securityId);

        if (groups.Count == 0)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return;
        }

        var snapshots = groups.Select(OrderGroupSnapshot.FromOrderGroup).ToList();
        var json = JsonSerializer.Serialize(snapshots, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    public IReadOnlyList<OrderGroup> Load(string securityId)
    {
        var filePath = GetFilePath(securityId);

        if (!File.Exists(filePath))
        {
            return [];
        }

        var json = File.ReadAllText(filePath);
        var snapshots = JsonSerializer.Deserialize<List<OrderGroupSnapshot>>(json, _jsonOptions);

        if (snapshots == null)
        {
            return [];
        }

        return snapshots.Select(s => s.ToOrderGroup()).ToList();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<OrderGroup>> LoadAll()
    {
        var result = new Dictionary<string, IReadOnlyList<OrderGroup>>();

        if (!Directory.Exists(_directory))
        {
            return result;
        }

        var jsonFiles = Directory.GetFiles(_directory, "*.json");

        foreach (var file in jsonFiles)
        {
            var securityId = FileNameToSecurityId(Path.GetFileNameWithoutExtension(file));
            var groups = LoadFromFile(file);

            if (groups.Count > 0)
            {
                result[securityId] = groups;
            }
        }

        return result;
    }

    public void Delete(string securityId)
    {
        var filePath = GetFilePath(securityId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private string GetFilePath(string securityId)
    {
        var sanitizedId = SanitizeSecurityId(securityId);
        return Path.Combine(_directory, $"{sanitizedId}.json");
    }

    private static string SanitizeSecurityId(string securityId) =>
        securityId.Replace('@', '_').Replace('/', '_').Replace('\\', '_');

    private static string FileNameToSecurityId(string fileName)
    {
        var index = fileName.IndexOf('_');
        if (index >= 0)
        {
            return string.Concat(fileName.AsSpan(0, index), "@", fileName.AsSpan(index + 1));
        }
        return fileName;
    }

    private IReadOnlyList<OrderGroup> LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        var json = File.ReadAllText(filePath);
        var snapshots = JsonSerializer.Deserialize<List<OrderGroupSnapshot>>(json, _jsonOptions);

        if (snapshots == null)
        {
            return [];
        }

        return snapshots.Select(s => s.ToOrderGroup()).ToList();
    }
}

internal sealed class OrderGroupSnapshot
{
    public required string GroupId { get; set; }
    public required string SecurityId { get; set; }
    public Sides Direction { get; set; }
    public OrderGroupState State { get; set; }
    public required GroupedOrderSnapshot OpeningOrder { get; set; }
    public required List<GroupedOrderSnapshot> ClosingOrders { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static OrderGroupSnapshot FromOrderGroup(OrderGroup group) => new()
    {
        GroupId = group.GroupId,
        SecurityId = group.SecurityId,
        Direction = group.Direction,
        State = group.State,
        OpeningOrder = GroupedOrderSnapshot.FromGroupedOrder(group.OpeningOrder),
        ClosingOrders = group.ClosingOrders.Select(GroupedOrderSnapshot.FromGroupedOrder).ToList(),
        CreatedAt = group.CreatedAt,
        ActivatedAt = group.ActivatedAt,
        CompletedAt = group.CompletedAt
    };

    public OrderGroup ToOrderGroup()
    {
        var openingOrder = OpeningOrder.ToGroupedOrder();
        var closingOrders = ClosingOrders.Select(s => s.ToGroupedOrder());

        var group = new OrderGroup(
            groupId: GroupId,
            securityId: SecurityId,
            direction: Direction,
            openingOrder: openingOrder,
            closingOrders: closingOrders);

        RestoreGroupState(group);

        return group;
    }

    private void RestoreGroupState(OrderGroup group)
    {
        switch (State)
        {
            case OrderGroupState.Active:
                group.MarkActivated();
                break;
            case OrderGroupState.Completed:
                group.MarkActivated();
                group.MarkCompleted();
                break;
            case OrderGroupState.Cancelled:
                group.MarkCancelled();
                break;
            case OrderGroupState.Closing:
                group.SetState(OrderGroupState.Closing);
                break;
        }
    }
}

internal sealed class GroupedOrderSnapshot
{
    public required string OrderId { get; set; }
    public GroupedOrderRole Role { get; set; }
    public decimal Price { get; set; }
    public decimal Volume { get; set; }
    public decimal FilledVolume { get; set; }
    public OrderTypes OrderType { get; set; }
    public GroupedOrderState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FilledAt { get; set; }

    public static GroupedOrderSnapshot FromGroupedOrder(GroupedOrder order) => new()
    {
        OrderId = order.OrderId,
        Role = order.Role,
        Price = order.Price,
        Volume = order.Volume,
        FilledVolume = order.FilledVolume,
        OrderType = order.OrderType,
        State = order.State,
        CreatedAt = order.CreatedAt,
        FilledAt = order.FilledAt
    };

    public GroupedOrder ToGroupedOrder()
    {
        var order = new GroupedOrder(
            orderId: OrderId,
            role: Role,
            price: Price,
            volume: Volume,
            orderType: OrderType);

        if (FilledVolume > 0)
        {
            order.AddFilledVolume(FilledVolume);
        }

        order.SetState(State);

        return order;
    }
}
