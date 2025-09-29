using System.Text.Json.Serialization;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies.Models;

public enum StrategyStatus
{
    NotStarted,
    Starting,
    Running,
    Stopping,
    Stopped,
    Error,
    Suspended
}

public record StrategyState(
    [property: JsonPropertyName("status")] StrategyStatus Status,
    [property: JsonPropertyName("startTime")] DateTimeOffset? StartTime,
    [property: JsonPropertyName("lastUpdateTime")] DateTimeOffset LastUpdateTime,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("activePositions")] int ActivePositions = 0,
    [property: JsonPropertyName("pendingOrders")] int PendingOrders = 0,
    [property: JsonPropertyName("lastTradeTime")] DateTimeOffset? LastTradeTime = null,
    [property: JsonPropertyName("parameters")] ImmutableDictionary<string, object?>? Parameters = null
)
{
    /// <summary>
    /// Initial strategy state
    /// </summary>
    public static readonly StrategyState Initial = new(
        Status: StrategyStatus.NotStarted,
        StartTime: null,
        LastUpdateTime: DateTimeOffset.UtcNow
    );

    /// <summary>
    /// Strategy uptime (if started)
    /// </summary>
    [JsonPropertyName("uptime")]
    public TimeSpan? Uptime => StartTime.HasValue ? LastUpdateTime - StartTime.Value : null;

    /// <summary>
    /// Whether strategy is actively trading
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive => Status == StrategyStatus.Running && !HasErrors;

    /// <summary>
    /// Whether strategy has errors
    /// </summary>
    [JsonPropertyName("hasErrors")]
    public bool HasErrors => Status == StrategyStatus.Error || !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Whether strategy can accept new orders
    /// </summary>
    [JsonPropertyName("canTrade")]
    public bool CanTrade => Status == StrategyStatus.Running && !HasErrors;

    /// <summary>
    /// Create a new state with updated status
    /// </summary>
    public StrategyState WithStatus(StrategyStatus newStatus, string? errorMessage = null) => this with
    {
        Status = newStatus,
        LastUpdateTime = DateTimeOffset.UtcNow,
        ErrorMessage = errorMessage ?? (newStatus == StrategyStatus.Error ? ErrorMessage : null),
        StartTime = newStatus == StrategyStatus.Running && StartTime == null ? DateTimeOffset.UtcNow : StartTime
    };

    /// <summary>
    /// Create a new state with updated positions and orders
    /// </summary>
    public StrategyState WithPositionsAndOrders(int activePositions, int pendingOrders) => this with
    {
        ActivePositions = activePositions,
        PendingOrders = pendingOrders,
        LastUpdateTime = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Create a new state with updated last trade time
    /// </summary>
    public StrategyState WithLastTrade(DateTimeOffset tradeTime) => this with
    {
        LastTradeTime = tradeTime,
        LastUpdateTime = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Create a new state with updated parameters
    /// </summary>
    public StrategyState WithParameters(ImmutableDictionary<string, object?> parameters) => this with
    {
        Parameters = parameters,
        LastUpdateTime = DateTimeOffset.UtcNow
    };
}

/// <summary>
/// Strategy state change event
/// </summary>
/// <param name="PreviousState">Previous strategy state</param>
/// <param name="NewState">New strategy state</param>
/// <param name="Timestamp">When the change occurred</param>
/// <param name="Reason">Reason for the state change</param>
public record StrategyStateChange(
    [property: JsonPropertyName("previousState")] StrategyState PreviousState,
    [property: JsonPropertyName("newState")] StrategyState NewState,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("reason")] string? Reason = null
)
{
    /// <summary>
    /// Whether this is a status change
    /// </summary>
    [JsonPropertyName("isStatusChange")]
    public bool IsStatusChange => PreviousState.Status != NewState.Status;

    /// <summary>
    /// Whether this is an error transition
    /// </summary>
    [JsonPropertyName("isErrorTransition")]
    public bool IsErrorTransition => !PreviousState.HasErrors && NewState.HasErrors;

    /// <summary>
    /// Whether this is a recovery transition
    /// </summary>
    [JsonPropertyName("isRecoveryTransition")]
    public bool IsRecoveryTransition => PreviousState.HasErrors && !NewState.HasErrors;
}