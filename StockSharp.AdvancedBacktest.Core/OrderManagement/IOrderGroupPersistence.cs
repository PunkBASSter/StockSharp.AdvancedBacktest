namespace StockSharp.AdvancedBacktest.OrderManagement;

public interface IOrderGroupPersistence
{
    void Save(string securityId, IReadOnlyList<OrderGroup> groups);

    IReadOnlyList<OrderGroup> Load(string securityId);

    IReadOnlyDictionary<string, IReadOnlyList<OrderGroup>> LoadAll();

    void Delete(string securityId);

    bool IsEnabled { get; }
}

public sealed class NullOrderGroupPersistence : IOrderGroupPersistence
{
    public static NullOrderGroupPersistence Instance { get; } = new();

    private NullOrderGroupPersistence() { }

    public bool IsEnabled => false;

    public void Save(string securityId, IReadOnlyList<OrderGroup> groups) { }

    public IReadOnlyList<OrderGroup> Load(string securityId) => [];

    public IReadOnlyDictionary<string, IReadOnlyList<OrderGroup>> LoadAll() =>
        new Dictionary<string, IReadOnlyList<OrderGroup>>();

    public void Delete(string securityId) { }
}
