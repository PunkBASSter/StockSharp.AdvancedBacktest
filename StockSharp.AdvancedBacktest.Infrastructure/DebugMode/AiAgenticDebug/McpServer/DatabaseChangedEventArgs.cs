namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public sealed class DatabaseChangedEventArgs : EventArgs
{
    public required string DatabasePath { get; init; }
    public required bool Exists { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
