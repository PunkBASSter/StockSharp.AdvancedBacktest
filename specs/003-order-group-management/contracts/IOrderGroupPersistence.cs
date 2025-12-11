// Contract specification for IOrderGroupPersistence
// Location: StockSharp.AdvancedBacktest.Core/OrderManagement/IOrderGroupPersistence.cs

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Abstraction for order group state persistence.
/// Implemented in Infrastructure for JSON file storage.
/// </summary>
public interface IOrderGroupPersistence
{
    /// <summary>
    /// Saves the current state of all order groups for a security.
    /// </summary>
    /// <param name="securityId">The security identifier.</param>
    /// <param name="groups">The order groups to persist.</param>
    void Save(string securityId, IReadOnlyList<OrderGroup> groups);

    /// <summary>
    /// Loads order groups for a security from persistent storage.
    /// </summary>
    /// <param name="securityId">The security identifier.</param>
    /// <returns>The loaded order groups, or empty list if none found.</returns>
    IReadOnlyList<OrderGroup> Load(string securityId);

    /// <summary>
    /// Loads all persisted order groups across all securities.
    /// </summary>
    /// <returns>Dictionary of security ID to order groups.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<OrderGroup>> LoadAll();

    /// <summary>
    /// Deletes persisted state for a security.
    /// </summary>
    /// <param name="securityId">The security identifier.</param>
    void Delete(string securityId);

    /// <summary>
    /// Checks if persistence is enabled (live mode vs backtest mode).
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Null implementation for backtest mode - no persistence.
/// </summary>
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
