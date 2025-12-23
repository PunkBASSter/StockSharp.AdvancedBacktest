namespace StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

/// <summary>
/// Interface for debug mode output destinations (human-readable files, AI event logs, etc.).
/// </summary>
public interface IDebugModeOutput : IDisposable
{
    /// <summary>
    /// Writes an event to the output destination.
    /// </summary>
    /// <param name="eventData">The event data to write.</param>
    /// <param name="displayTimestamp">The timestamp to display (remapped to main TF if necessary).</param>
    void Write(object eventData, DateTimeOffset displayTimestamp);

    /// <summary>
    /// Flushes any buffered output.
    /// </summary>
    void Flush();
}
