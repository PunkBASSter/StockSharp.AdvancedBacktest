using System.Threading;
using System.Collections.Generic;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Main orchestrator for debug mode event capture and export.
/// Coordinates event buffering and file writing for real-time visualization.
/// </summary>
public class DebugModeExporter : IDisposable
{
	private readonly string _outputPath;
	private readonly int _flushIntervalMs;
	private readonly int _maxFileSizeMB;

	private DebugEventBuffer? _buffer;
	private FileBasedWriter? _writer;
	private CustomStrategyBase? _strategy;
	private long _sequenceNumber = 0;
	private long _eventCount = 0;
	private bool _disposed;

	/// <summary>
	/// Creates a new debug mode exporter.
	/// </summary>
	/// <param name="outputPath">Base path for JSONL output files</param>
	/// <param name="flushIntervalMs">Buffer flush interval in milliseconds (default: 500ms to match browser polling)</param>
	/// <param name="maxFileSizeMB">Maximum file size before rotation (default: 10MB)</param>
	public DebugModeExporter(string outputPath, int flushIntervalMs = 500, int maxFileSizeMB = 10)
	{
		if (string.IsNullOrWhiteSpace(outputPath))
			throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

		if (flushIntervalMs <= 0)
			throw new ArgumentException("Flush interval must be positive", nameof(flushIntervalMs));

		if (maxFileSizeMB <= 0)
			throw new ArgumentException("Max file size must be positive", nameof(maxFileSizeMB));

		_outputPath = outputPath;
		_flushIntervalMs = flushIntervalMs;
		_maxFileSizeMB = maxFileSizeMB;
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
		_writer = new FileBasedWriter(_outputPath, _maxFileSizeMB);

		_strategy.LogInfo($"Debug mode initialized. Output: {_outputPath}, Flush interval: {_flushIntervalMs}ms");
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
			// Unsubscribe from events
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

	/// <summary>
	/// Gets next sequence number for event ordering.
	/// Thread-safe using Interlocked.Increment.
	/// </summary>
	/// <returns>Next sequence number</returns>
	protected long GetNextSequence()
	{
		return Interlocked.Increment(ref _sequenceNumber);
	}

	#region Event Capture Methods (Stubs for DM-03)

	/// <summary>
	/// Captures a candle update event.
	/// Implementation will be added in DM-03.
	/// </summary>
	/// <param name="candle">Candle data point to capture</param>
	public void CaptureCandle(CandleDataPoint candle)
	{
		if (!IsInitialized || _disposed)
			return;

		if (candle == null)
			throw new ArgumentNullException(nameof(candle));

		// Set sequence number
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
