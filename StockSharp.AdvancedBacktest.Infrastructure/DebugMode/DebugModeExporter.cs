using System.Threading;
using System.Collections.Generic;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Main orchestrator for debug mode event capture and export.
/// Coordinates event buffering and file writing for real-time visualization.
/// Uses IndicatorDataExtractor for consistent indicator data extraction.
/// </summary>
public class DebugModeExporter : IDisposable
{
    private readonly string _outputPath;
    private readonly int _flushIntervalMs;
    private readonly IndicatorDataExtractor _extractor;

    private DebugEventBuffer? _buffer;
    private FileBasedWriter? _writer;
    private CustomStrategyBase? _strategy;
    private long _sequenceNumber = 0;
    private long _eventCount = 0;
    private bool _disposed;
    private readonly List<(IIndicator indicator, Action<IIndicatorValue, IIndicatorValue> handler)> _indicatorSubscriptions = new();

    // Candle interval tracking for shift-aware indicator export
    private TimeSpan? _candleInterval;           // Detected or configured interval
    private DateTimeOffset? _lastCandleTime;     // Last candle timestamp for auto-detection
    private TimeSpan? _configuredInterval;       // Explicitly provided interval (optional)

    /// <summary>
    /// Creates a new debug mode exporter.
    /// </summary>
    /// <param name="outputPath">Path for JSONL output file (e.g., "debug/latest.jsonl")</param>
    /// <param name="flushIntervalMs">Buffer flush interval in milliseconds (default: 500ms to match browser polling)</param>
    public DebugModeExporter(string outputPath, int flushIntervalMs = 500)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

        if (flushIntervalMs <= 0)
            throw new ArgumentException("Flush interval must be positive", nameof(flushIntervalMs));

        _outputPath = outputPath;
        _flushIntervalMs = flushIntervalMs;
        _extractor = new IndicatorDataExtractor();
    }

    /// <summary>
    /// Indicates whether the exporter has been initialized.
    /// </summary>
    public bool IsInitialized => _buffer != null && _writer != null;

    /// <summary>
    /// Base output path for JSONL files.
    /// </summary>
    public string OutputPath => _outputPath;

    /// <summary>
    /// Total number of events processed.
    /// </summary>
    public long EventCount => Interlocked.Read(ref _eventCount);

    /// <summary>
    /// Current sequence number (for diagnostics).
    /// </summary>
    public long CurrentSequence => Interlocked.Read(ref _sequenceNumber);

    /// <summary>
    /// Detected or configured candle interval.
    /// Null until at least 2 candles have been captured or interval was explicitly set during initialization.
    /// Used for calculating correct timestamps for shifted indicators (e.g., ZigZag extrema).
    /// </summary>
    public TimeSpan? CandleInterval => _candleInterval;

    /// <summary>
    /// Initializes debug mode hooks for the given strategy.
    /// Sets up event buffer, file writer, and event subscriptions.
    /// </summary>
    /// <param name="strategy">Strategy to attach debug mode to</param>
    public void Initialize(CustomStrategyBase strategy)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DebugModeExporter));

        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));

        // Store strategy reference
        _strategy = strategy;

        // Reset sequence number
        Interlocked.Exchange(ref _sequenceNumber, 0);
        Interlocked.Exchange(ref _eventCount, 0);

        // Create event buffer with time-based flushing
        _buffer = new DebugEventBuffer(_flushIntervalMs);

        // Subscribe to buffer flush events
        _buffer.OnFlush += OnBufferFlushed;

        // Create file writer
        _writer = new FileBasedWriter(_outputPath);

        _strategy.LogInfo($"Debug mode initialized. Output: {_outputPath}, Flush interval: {_flushIntervalMs}ms");
    }

    /// <summary>
    /// Initializes debug mode hooks for the given strategy with optional candle interval.
    /// If candle interval is provided, it will be used immediately and validated against auto-detected intervals.
    /// </summary>
    /// <param name="strategy">Strategy to attach debug mode to</param>
    /// <param name="candleInterval">Optional candle interval for validation (e.g., TimeSpan.FromHours(1))</param>
    public void Initialize(CustomStrategyBase strategy, TimeSpan? candleInterval)
    {
        // Call base initialization
        Initialize(strategy);

        // Store and use configured interval if provided
        if (candleInterval.HasValue)
        {
            _configuredInterval = candleInterval.Value;
            _candleInterval = candleInterval.Value;
            _strategy?.LogInfo($"Debug mode: Candle interval configured to {candleInterval.Value}");
        }
    }

    /// <summary>
    /// Handles buffer flush events by writing buffered events to file.
    /// </summary>
    /// <param name="events">Dictionary of event type to list of event data</param>
    private void OnBufferFlushed(Dictionary<string, List<object>> events)
    {
        if (_writer == null || events == null || events.Count == 0)
            return;

        try
        {
            // Write batch to JSONL file
            _writer.WriteBatch(events);

            // Update event count
            var totalEvents = 0;
            foreach (var eventList in events.Values)
            {
                totalEvents += eventList.Count;
            }

            Interlocked.Add(ref _eventCount, totalEvents);
        }
        catch (Exception ex)
        {
            // Log error but don't crash strategy
            _strategy?.LogError($"Failed to write debug events: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleanup and flush remaining events.
    /// Should be called when strategy stops.
    /// </summary>
    public void Cleanup()
    {
        if (!IsInitialized)
            return;

        try
        {
            // Unsubscribe from indicator events
            foreach (var (indicator, handler) in _indicatorSubscriptions)
            {
                try
                {
                    indicator.Changed -= handler;
                }
                catch
                {
                    // Ignore unsubscribe errors
                }
            }
            _indicatorSubscriptions.Clear();

            // Reset interval tracking
            _candleInterval = null;
            _lastCandleTime = null;
            _configuredInterval = null;

            // Unsubscribe from buffer events
            if (_buffer != null)
            {
                _buffer.OnFlush -= OnBufferFlushed;
            }

            // Dispose buffer (triggers final flush)
            _buffer?.Dispose();
            _buffer = null;

            // Dispose writer (closes file)
            _writer?.Dispose();
            _writer = null;

            _strategy?.LogInfo($"Debug mode cleanup completed. Total events: {EventCount}");
            _strategy = null;
        }
        catch (Exception ex)
        {
            _strategy?.LogError($"Error during debug mode cleanup: {ex.Message}");
        }
    }

    protected long GetNextSequence()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }


    #region Indicator Subscription Methods

    public DateTimeOffset GetAdjustedIndicatorTimestamp(IIndicatorValue value, DateTimeOffset fallbackTime)
    {
        if (value == null)
            return fallbackTime;

        if (IndicatorValueHelper.TryGetShift(value, out int shift) && shift > 0)
        {
            if (_candleInterval.HasValue)
            {
                var adjustedTime = IndicatorValueHelper.GetAdjustedTimestamp(value, _candleInterval);
                _strategy?.LogDebug($"Adjusted indicator timestamp: {value.Time} - {shift} candles = {adjustedTime}");
                return adjustedTime;
            }
            else
            {
                _strategy?.LogWarning($"Cannot apply shift correction: candle interval not yet detected (shift={shift})");
                return value.Time;
            }
        }

        return value.Time;
    }

    public IndicatorDataPoint? CreateIndicatorDataPoint(IIndicatorValue value)
    {
        if (value == null)
            return null;

        var dataPoint = _extractor.ExtractFromValue(value, _candleInterval);

        if (dataPoint != null)
        {
            dataPoint.SequenceNumber = GetNextSequence();
        }

        return dataPoint;
    }

    /// <summary>
    /// Subscribes to an indicator's Changed event for automatic value capture.
    /// Only captures values when indicator is formed (has enough data).
    /// </summary>
    /// <param name="indicator">Indicator to subscribe to</param>
    public void SubscribeToIndicator(IIndicator indicator)
    {
        if (!IsInitialized || _disposed)
            return;

        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        try
        {
            // Create event handler
            Action<IIndicatorValue, IIndicatorValue> handler = (input, result) =>
            {
                try
                {
                    var dataPoint = CreateIndicatorDataPoint(result);

                    if (dataPoint != null)
                    {
                        CaptureIndicator(indicator.Name, dataPoint);
                    }
                }
                catch (Exception ex)
                {
                    _strategy?.LogError($"Error capturing indicator {indicator.Name}: {ex.Message}");
                }
            };

            // Subscribe to Changed event
            indicator.Changed += handler;

            // Track subscription for cleanup
            _indicatorSubscriptions.Add((indicator, handler));

            _strategy?.LogDebug($"Subscribed to indicator: {indicator.Name}");
        }
        catch (Exception ex)
        {
            _strategy?.LogError($"Failed to subscribe to indicator {indicator.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Automatically discovers and subscribes to all indicators in a collection.
    /// This is called from CustomStrategyBase.OnStarted to capture all strategy indicators.
    /// </summary>
    /// <param name="indicators">Indicator collection from strategy</param>
    public void SubscribeToIndicators(IEnumerable<IIndicator> indicators)
    {
        if (!IsInitialized || _disposed)
            return;

        if (indicators == null)
            throw new ArgumentNullException(nameof(indicators));

        try
        {
            var count = 0;
            // Iterate through all indicators
            foreach (var indicator in indicators)
            {
                SubscribeToIndicator(indicator);
                count++;
            }

            _strategy?.LogInfo($"Subscribed to {count} indicators for debug mode");
        }
        catch (Exception ex)
        {
            _strategy?.LogError($"Error subscribing to indicators: {ex.Message}");
        }
    }

    #endregion

    #region Event Capture Methods

    /// <summary>
    /// Captures a candle update event from StockSharp candle message.
    /// Automatically extracts OHLCV data and security identifier.
    /// </summary>
    /// <param name="candle">ICandleMessage from StockSharp</param>
    /// <param name="securityId">Security identifier</param>
    public void CaptureCandle(ICandleMessage candle, SecurityId securityId)
    {
        if (!IsInitialized || _disposed)
            return;

        if (candle == null)
            throw new ArgumentNullException(nameof(candle));

        try
        {
            // Auto-detect candle interval from consecutive candles
            if (_lastCandleTime.HasValue)
            {
                var detectedInterval = candle.OpenTime - _lastCandleTime.Value;

                // If no interval set yet, use detected value
                if (!_candleInterval.HasValue)
                {
                    _candleInterval = detectedInterval;
                    _strategy?.LogDebug($"Auto-detected candle interval: {detectedInterval}");
                }
                // If configured interval exists, validate detection matches
                else if (_configuredInterval.HasValue && _candleInterval.Value != detectedInterval)
                {
                    _strategy?.LogWarning($"Detected candle interval ({detectedInterval}) differs from configured interval ({_configuredInterval.Value})");
                }
            }
            _lastCandleTime = candle.OpenTime;

            var dataPoint = new CandleDataPoint
            {
                Time = new DateTimeOffset(candle.OpenTime, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                Open = (double)candle.OpenPrice,
                High = (double)candle.HighPrice,
                Low = (double)candle.LowPrice,
                Close = (double)candle.ClosePrice,
                Volume = (double)candle.TotalVolume,
                SecurityId = securityId.ToStringId(),
                SequenceNumber = GetNextSequence()
            };

            _buffer!.Add("candle", dataPoint);
        }
        catch (Exception ex)
        {
            _strategy?.LogError($"Error capturing candle: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures a candle data point directly (used by tests).
    /// </summary>
    /// <param name="candle">Candle data point to capture</param>
    public void CaptureCandle(CandleDataPoint candle)
    {
        if (!IsInitialized || _disposed)
            return;

        if (candle == null)
            throw new ArgumentNullException(nameof(candle));

        // Set sequence number if not already set
        if (candle.SequenceNumber == null)
            candle.SequenceNumber = GetNextSequence();

        // Add to buffer
        _buffer!.Add("candle", candle);
    }

    /// <summary>
    /// Captures an indicator value update.
    /// Implementation will be added in DM-03.
    /// </summary>
    /// <param name="indicatorName">Name of the indicator</param>
    /// <param name="indicator">Indicator data point to capture</param>
    public void CaptureIndicator(string indicatorName, IndicatorDataPoint indicator)
    {
        if (!IsInitialized || _disposed)
            return;

        if (string.IsNullOrEmpty(indicatorName))
            throw new ArgumentException("Indicator name cannot be null or empty", nameof(indicatorName));

        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        // Set sequence number
        indicator.SequenceNumber = GetNextSequence();

        // Add to buffer with indicator-specific event type
        _buffer!.Add($"indicator_{indicatorName}", indicator);
    }

    /// <summary>
    /// Captures a trade execution event.
    /// Implementation will be added in DM-03.
    /// </summary>
    /// <param name="trade">Trade data point to capture</param>
    public void CaptureTrade(TradeDataPoint trade)
    {
        if (!IsInitialized || _disposed)
            return;

        if (trade == null)
            throw new ArgumentNullException(nameof(trade));

        // Set sequence number
        trade.SequenceNumber = GetNextSequence();

        // Add to buffer
        _buffer!.Add("trade", trade);
    }

    /// <summary>
    /// Captures a strategy state update.
    /// Implementation will be added in DM-03.
    /// </summary>
    /// <param name="state">State data point to capture</param>
    public void CaptureState(StateDataPoint state)
    {
        if (!IsInitialized || _disposed)
            return;

        if (state == null)
            throw new ArgumentNullException(nameof(state));

        // Set sequence number
        state.SequenceNumber = GetNextSequence();

        // Add to buffer
        _buffer!.Add("state", state);
    }

    #endregion

#if DEBUG
    public void FlushBeforeCandle()
    {
        if (!IsInitialized || _disposed)
            return;

        try
        {
            _buffer!.FlushSynchronously();
            _strategy?.LogDebug($"Debug mode: Synchronously flushed {EventCount} events before new candle");
        }
        catch (Exception ex)
        {
            _strategy?.LogError($"Failed to flush debug events before candle: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// Disposes the exporter and performs cleanup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Cleanup();
    }
}
