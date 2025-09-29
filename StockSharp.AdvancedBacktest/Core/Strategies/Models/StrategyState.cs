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
    public static readonly StrategyState Initial = new(
        Status: StrategyStatus.NotStarted,
        StartTime: null,
        LastUpdateTime: DateTimeOffset.UtcNow
    );

    [JsonPropertyName("uptime")]
    public TimeSpan? Uptime => StartTime.HasValue ? LastUpdateTime - StartTime.Value : null;

    [JsonPropertyName("isActive")]
    public bool IsActive => Status == StrategyStatus.Running && !HasErrors;

    [JsonPropertyName("hasErrors")]
    public bool HasErrors => Status == StrategyStatus.Error || !string.IsNullOrEmpty(ErrorMessage);

    [JsonPropertyName("canTrade")]
    public bool CanTrade => Status == StrategyStatus.Running && !HasErrors;

    public StrategyState WithStatus(StrategyStatus newStatus, string? errorMessage = null) => this with
    {
        Status = newStatus,
        LastUpdateTime = DateTimeOffset.UtcNow,
        ErrorMessage = errorMessage ?? (newStatus == StrategyStatus.Error ? ErrorMessage : null),
        StartTime = newStatus == StrategyStatus.Running && StartTime == null ? DateTimeOffset.UtcNow : StartTime
    };

    public StrategyState WithPositionsAndOrders(int activePositions, int pendingOrders) => this with
    {
        ActivePositions = activePositions,
        PendingOrders = pendingOrders,
        LastUpdateTime = DateTimeOffset.UtcNow
    };

    public StrategyState WithLastTrade(DateTimeOffset tradeTime) => this with
    {
        LastTradeTime = tradeTime,
        LastUpdateTime = DateTimeOffset.UtcNow
    };

    public StrategyState WithParameters(ImmutableDictionary<string, object?> parameters) => this with
    {
        Parameters = parameters,
        LastUpdateTime = DateTimeOffset.UtcNow
    };
}

public record StrategyStateChange(
    [property: JsonPropertyName("previousState")] StrategyState PreviousState,
    [property: JsonPropertyName("newState")] StrategyState NewState,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("reason")] string? Reason = null
)
{
    [JsonPropertyName("isStatusChange")]
    public bool IsStatusChange => PreviousState.Status != NewState.Status;

    [JsonPropertyName("isErrorTransition")]
    public bool IsErrorTransition => !PreviousState.HasErrors && NewState.HasErrors;

    [JsonPropertyName("isRecoveryTransition")]
    public bool IsRecoveryTransition => PreviousState.HasErrors && !NewState.HasErrors;
}