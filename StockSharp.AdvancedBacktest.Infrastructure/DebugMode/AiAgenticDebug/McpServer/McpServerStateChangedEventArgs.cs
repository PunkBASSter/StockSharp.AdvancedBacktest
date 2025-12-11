namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public sealed class McpServerStateChangedEventArgs : EventArgs
{
    public required McpServerState OldState { get; init; }
    public required McpServerState NewState { get; init; }
    public string? Message { get; init; }
}
