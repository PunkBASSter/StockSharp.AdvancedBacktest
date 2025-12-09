namespace StockSharp.AdvancedBacktest;

/// <summary>
/// Abstraction for debug event logging. Allows Core trading classes to emit
/// debug events without knowing the concrete implementation (file, SQLite, etc.).
/// </summary>
public interface IDebugEventSink
{
    void LogEvent(string category, string eventType, object data);
    void Flush();
}
