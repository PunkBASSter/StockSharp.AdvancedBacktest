namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public sealed class McpServerLifecycleConfig
{
    public required string DatabasePath { get; init; }
    public bool AutoStart { get; init; } = true;
    public int StartupTimeoutMs { get; init; } = 10000;
    public int ShutdownTimeoutMs { get; init; } = 5000;
    public int ReconnectDelayMs { get; init; } = 1000;
}
