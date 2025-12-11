// Contract specification for IOrderGroupManager
// Location: StockSharp.AdvancedBacktest.Core/OrderManagement/IOrderGroupManager.cs

using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Manages order groups for a trading strategy, supporting multiple groups per security
/// with configurable limits and state tracking.
/// </summary>
public interface IOrderGroupManager
{
    /// <summary>
    /// Gets the current order group limits configuration.
    /// </summary>
    OrderGroupLimits Limits { get; }

    /// <summary>
    /// Creates a new order group from an extended trade signal.
    /// Places the opening order immediately.
    /// </summary>
    /// <param name="signal">The trade signal defining the order group.</param>
    /// <param name="throwIfNotMatchingVolume">
    /// If true, throws when closing volumes don't sum to opening volume.
    /// Overrides Limits.ThrowIfNotMatchingVolume for this call.
    /// </param>
    /// <returns>The created order group in Pending state.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when max groups limit or risk limit would be exceeded.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when signal validation fails or volumes don't match (if validation enabled).
    /// </exception>
    OrderGroup CreateOrderGroup(ExtendedTradeSignal signal, bool? throwIfNotMatchingVolume = null);

    /// <summary>
    /// Gets all active order groups, optionally filtered by security.
    /// </summary>
    /// <param name="securityId">Optional security filter.</param>
    /// <returns>Read-only list of active groups.</returns>
    IReadOnlyList<OrderGroup> GetActiveGroups(string? securityId = null);

    /// <summary>
    /// Gets an order group by its unique identifier.
    /// </summary>
    /// <param name="groupId">The group identifier.</param>
    /// <returns>The order group, or null if not found.</returns>
    OrderGroup? GetGroupById(string groupId);

    /// <summary>
    /// Adjusts the activation price of a pending order within a group.
    /// Cancels the existing order and places a new one at the new price.
    /// </summary>
    /// <param name="groupId">The group containing the order.</param>
    /// <param name="orderId">The order identifier within the group.</param>
    /// <param name="newPrice">The new activation price.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when order is not in Pending/Active state.
    /// </exception>
    void AdjustOrderPrice(string groupId, string orderId, decimal newPrice);

    /// <summary>
    /// Closes an order group by cancelling pending orders and closing any open position.
    /// </summary>
    /// <param name="groupId">The group to close.</param>
    void CloseGroup(string groupId);

    /// <summary>
    /// Closes all order groups, optionally filtered by security.
    /// </summary>
    /// <param name="securityId">Optional security filter.</param>
    void CloseAllGroups(string? securityId = null);

    /// <summary>
    /// Handles an order fill event. Call from strategy's OnOwnTradeReceived.
    /// </summary>
    /// <param name="order">The filled order.</param>
    /// <param name="trade">The trade details.</param>
    void OnOrderFilled(Order order, MyTrade trade);

    /// <summary>
    /// Handles an order cancellation event.
    /// </summary>
    /// <param name="order">The cancelled order.</param>
    void OnOrderCancelled(Order order);

    /// <summary>
    /// Handles an order rejection event.
    /// </summary>
    /// <param name="order">The rejected order.</param>
    void OnOrderRejected(Order order);

    /// <summary>
    /// Resets the manager state. Call from strategy's OnReseted.
    /// </summary>
    void Reset();

    /// <summary>
    /// Calculates the risk percentage for a potential order group.
    /// </summary>
    /// <param name="entryPrice">Entry price.</param>
    /// <param name="volume">Position volume.</param>
    /// <param name="stopLossPrice">Stop-loss price.</param>
    /// <param name="currentEquity">Current account equity.</param>
    /// <returns>Risk as percentage of equity (e.g., 2.5 for 2.5%).</returns>
    decimal CalculateRiskPercent(decimal entryPrice, decimal volume, decimal stopLossPrice, decimal currentEquity);

    /// <summary>
    /// Raised when an opening order is filled and closing orders are placed.
    /// </summary>
    event Action<OrderGroup, GroupedOrder>? OrderActivated;

    /// <summary>
    /// Raised when all closing orders are filled and the group is complete.
    /// </summary>
    event Action<OrderGroup>? GroupCompleted;

    /// <summary>
    /// Raised when a group is cancelled.
    /// </summary>
    event Action<OrderGroup>? GroupCancelled;

    /// <summary>
    /// Raised when an order within a group is rejected.
    /// </summary>
    event Action<OrderGroup, GroupedOrder>? OrderRejected;
}
