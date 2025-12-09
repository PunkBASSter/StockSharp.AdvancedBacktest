namespace StockSharp.AdvancedBacktest;

public sealed class NullDebugEventSink : IDebugEventSink
{
    public static readonly NullDebugEventSink Instance = new();
    
    private NullDebugEventSink() { }
    public void LogEvent(string category, string eventType, object data)
    {
    }

    public void Flush()
    {
    }
}
