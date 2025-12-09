// Contract: IDebugEventSink - Debug logging abstraction for Core assembly
// This file defines the interface that Core uses for debug event logging.
// Infrastructure assembly provides concrete implementations.

namespace StockSharp.AdvancedBacktest.Core
{
    /// <summary>
    /// Abstraction for debug event logging. Allows Core trading classes to emit
    /// debug events without knowing the concrete implementation (file, SQLite, etc.).
    /// </summary>
    public interface IDebugEventSink
    {
        /// <summary>
        /// Logs a debug event with category, type, and associated data.
        /// </summary>
        /// <param name="category">Event category (e.g., "Candle", "Trade", "Indicator", "State")</param>
        /// <param name="eventType">Specific event type within category (e.g., "New", "Updated", "Closed")</param>
        /// <param name="data">Event payload - will be serialized by implementation</param>
        void LogEvent(string category, string eventType, object data);

        /// <summary>
        /// Flushes any buffered events to the underlying storage.
        /// Called at end of backtest or on explicit flush request.
        /// </summary>
        void Flush();
    }

    /// <summary>
    /// Null object implementation of IDebugEventSink.
    /// Used when debug logging is not configured - all operations are no-ops.
    /// Thread-safe singleton.
    /// </summary>
    public sealed class NullDebugEventSink : IDebugEventSink
    {
        /// <summary>
        /// Singleton instance. Use this instead of creating new instances.
        /// </summary>
        public static readonly NullDebugEventSink Instance = new();

        private NullDebugEventSink() { }

        /// <inheritdoc />
        public void LogEvent(string category, string eventType, object data)
        {
            // No-op by design
        }

        /// <inheritdoc />
        public void Flush()
        {
            // No-op by design
        }
    }
}
