using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Buffers debug events using time-based flushing instead of count-based.
/// Ensures all events during polling interval are captured, even when debugging with breakpoints.
/// </summary>
public class DebugEventBuffer : IDisposable
{
    private readonly Dictionary<string, List<object>> _buffers = new();
    private readonly Timer _flushTimer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Fired when buffer is flushed with accumulated events.
    /// Dictionary key is event type (e.g., "candle", "trade"), value is list of events of that type.
    /// </summary>
    public event Action<Dictionary<string, List<object>>>? OnFlush;

    /// <summary>
    /// Creates a new debug event buffer with time-based flushing.
    /// </summary>
    /// <param name="flushIntervalMs">Flush interval in milliseconds (default: 500ms to match polling interval)</param>
    public DebugEventBuffer(int flushIntervalMs = 500)
    {
        if (flushIntervalMs <= 0)
            throw new ArgumentException("Flush interval must be positive", nameof(flushIntervalMs));

        // Time-based flush (not count-based!)
        // Timer continues even when debugger hits breakpoint
        _flushTimer = new Timer(
            _ => Flush(),
            null,
            flushIntervalMs,
            flushIntervalMs);
    }

    /// <summary>
    /// Adds an event to the buffer.
    /// </summary>
    /// <param name="eventType">Type of event (e.g., "candle", "trade", "indicator_SMA_20")</param>
    /// <param name="eventData">Event data object (will be serialized later by FileBasedWriter)</param>
    public void Add(string eventType, object eventData)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DebugEventBuffer));

        if (string.IsNullOrEmpty(eventType))
            throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        lock (_lock)
        {
            if (!_buffers.ContainsKey(eventType))
            {
                _buffers[eventType] = new List<object>();
            }

            _buffers[eventType].Add(eventData);
        }
    }

    /// <summary>
    /// Manually flushes the buffer, firing OnFlush event with accumulated events.
    /// Called automatically by timer, but can also be called manually or on disposal.
    /// </summary>
    public void Flush()
    {
        if (_disposed)
            return;

        Dictionary<string, List<object>> eventsToFlush;

        lock (_lock)
        {
            // Don't flush if buffer is empty
            if (_buffers.Count == 0 || _buffers.All(kvp => kvp.Value.Count == 0))
                return;

            // Create snapshot of current buffer state
            eventsToFlush = new Dictionary<string, List<object>>();
            foreach (var (eventType, eventList) in _buffers)
            {
                if (eventList.Count > 0)
                {
                    eventsToFlush[eventType] = new List<object>(eventList);
                }
            }

            // Clear buffers for next interval
            _buffers.Clear();
        }

        // Fire event asynchronously to not block strategy execution
        // OnFlush handlers (e.g., FileBasedWriter) will process events in background
        if (OnFlush != null)
        {
            Task.Run(() =>
            {
                try
                {
                    OnFlush.Invoke(eventsToFlush);
                }
                catch
                {
                    // Swallow exceptions in event handlers to prevent crashing strategy
                    // TODO: Consider logging to strategy logger when integrated
                }
            });
        }
    }

#if DEBUG
    public void FlushSynchronously()
    {
        if (_disposed)
            return;

        Dictionary<string, List<object>> eventsToFlush;

        lock (_lock)
        {
            // Don't flush if buffer is empty
            if (_buffers.Count == 0 || _buffers.All(kvp => kvp.Value.Count == 0))
                return;

            // Create snapshot of current buffer state
            eventsToFlush = new Dictionary<string, List<object>>();
            foreach (var (eventType, eventList) in _buffers)
            {
                if (eventList.Count > 0)
                {
                    eventsToFlush[eventType] = new List<object>(eventList);
                }
            }

            // Clear buffers for next interval
            _buffers.Clear();
        }

        // Fire event SYNCHRONOUSLY (not Task.Run) to ensure completion before returning
        // This blocks until file write completes, guaranteeing events are on disk
        if (OnFlush != null)
        {
            try
            {
                OnFlush.Invoke(eventsToFlush);
            }
            catch
            {
                // Swallow exceptions in event handlers to prevent crashing strategy
            }
        }
    }
#endif

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop timer first
        _flushTimer?.Dispose();

        // Perform final synchronous flush to ensure no events are lost
        lock (_lock)
        {
            if (_buffers.Count > 0 && _buffers.Any(kvp => kvp.Value.Count > 0))
            {
                var eventsToFlush = new Dictionary<string, List<object>>();
                foreach (var (eventType, eventList) in _buffers)
                {
                    if (eventList.Count > 0)
                    {
                        eventsToFlush[eventType] = new List<object>(eventList);
                    }
                }

                _buffers.Clear();

                // Final flush is synchronous to ensure completion before disposal
                OnFlush?.Invoke(eventsToFlush);
            }
        }
    }
}
