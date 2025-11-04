using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Buffers debug events using time-based flushing instead of count-based.
/// Ensures all events during polling interval are captured, even when debugging with breakpoints.
/// Events are grouped by candle to ensure correct association.
/// </summary>
public class DebugEventBuffer : IDisposable
{
    // Stores events grouped by candle time, then by event type
    private readonly Dictionary<DateTimeOffset, Dictionary<string, List<object>>> _candleBuffers = [];
    private readonly Timer _flushTimer;
    private readonly Lock _lock = new();
    private bool _disposed;
    private DateTimeOffset _currentCandleTime = DateTimeOffset.MinValue;

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
    /// Sets the current candle time context for subsequent events.
    /// All events added after this call will be associated with this candle.
    /// </summary>
    /// <param name="candleTime">Timestamp of the current candle</param>
    public void SetCurrentCandle(DateTimeOffset candleTime)
    {
        lock (_lock)
        {
            _currentCandleTime = candleTime;
        }
    }

    /// <summary>
    /// Adds an event to the buffer, associating it with the current candle.
    /// If no candle has been set via SetCurrentCandle(), uses a default candle time for backward compatibility.
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
            // Use default candle time if not set (backward compatibility for tests without candle context)
            var candleTime = _currentCandleTime == DateTimeOffset.MinValue
                ? DateTimeOffset.MaxValue  // Use MaxValue as a "default" bucket that always flushes
                : _currentCandleTime;

            // Get or create buffer for current candle
            if (!_candleBuffers.ContainsKey(candleTime))
            {
                _candleBuffers[candleTime] = [];
            }

            var candleBuffer = _candleBuffers[candleTime];

            if (!candleBuffer.ContainsKey(eventType))
            {
                candleBuffer[eventType] = [];
            }

            candleBuffer[eventType].Add(eventData);
        }
    }

    /// <summary>
    /// Manually flushes completed candles, firing OnFlush event with accumulated events.
    /// Only flushes candles that are no longer current (to ensure all events are captured).
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
            if (_candleBuffers.Count == 0)
                return;

            // Flush all candles except the current one (which may still be accumulating events)
            eventsToFlush = [];
            var candlesToFlush = _candleBuffers.Keys
                .Where(candleTime => candleTime != _currentCandleTime)
                .OrderBy(candleTime => candleTime)
                .ToList();

            foreach (var candleTime in candlesToFlush)
            {
                var candleBuffer = _candleBuffers[candleTime];
                foreach (var (eventType, eventList) in candleBuffer)
                {
                    if (eventList.Count > 0)
                    {
                        if (!eventsToFlush.ContainsKey(eventType))
                        {
                            eventsToFlush[eventType] = new List<object>();
                        }
                        eventsToFlush[eventType].AddRange(eventList);
                    }
                }

                // Remove flushed candle from buffer
                _candleBuffers.Remove(candleTime);
            }

            // If nothing to flush, return early
            if (eventsToFlush.Count == 0)
                return;
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
    /// <summary>
    /// Synchronously flushes completed candles, ensuring events are on disk before returning.
    /// Only flushes candles that are no longer current to ensure correct event association.
    /// </summary>
    public void FlushSynchronously()
    {
        if (_disposed)
            return;

        Dictionary<string, List<object>> eventsToFlush;

        lock (_lock)
        {
            // Don't flush if buffer is empty
            if (_candleBuffers.Count == 0)
                return;

            // Flush all candles except the current one (which may still be accumulating events)
            eventsToFlush = new Dictionary<string, List<object>>();
            var candlesToFlush = _candleBuffers.Keys
                .Where(candleTime => candleTime != _currentCandleTime)
                .OrderBy(candleTime => candleTime)
                .ToList();

            foreach (var candleTime in candlesToFlush)
            {
                var candleBuffer = _candleBuffers[candleTime];
                foreach (var (eventType, eventList) in candleBuffer)
                {
                    if (eventList.Count > 0)
                    {
                        if (!eventsToFlush.ContainsKey(eventType))
                        {
                            eventsToFlush[eventType] = new List<object>();
                        }
                        eventsToFlush[eventType].AddRange(eventList);
                    }
                }

                // Remove flushed candle from buffer
                _candleBuffers.Remove(candleTime);
            }

            // If nothing to flush, return early
            if (eventsToFlush.Count == 0)
                return;
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
            if (_candleBuffers.Count > 0)
            {
                var eventsToFlush = new Dictionary<string, List<object>>();

                // Flush ALL candles including current one on disposal
                foreach (var (candleTime, candleBuffer) in _candleBuffers.OrderBy(kvp => kvp.Key))
                {
                    foreach (var (eventType, eventList) in candleBuffer)
                    {
                        if (eventList.Count > 0)
                        {
                            if (!eventsToFlush.ContainsKey(eventType))
                            {
                                eventsToFlush[eventType] = new List<object>();
                            }
                            eventsToFlush[eventType].AddRange(eventList);
                        }
                    }
                }

                _candleBuffers.Clear();

                // Final flush is synchronous to ensure completion before disposal
                if (eventsToFlush.Count > 0)
                {
                    OnFlush?.Invoke(eventsToFlush);
                }
            }
        }
    }
}
