using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Writes debug events to JSONL (JSON Lines) files with automatic rotation.
/// Thread-safe for concurrent writes from event buffer.
/// JSONL format allows browser to read file while it's being written.
/// </summary>
public class FileBasedWriter : IDisposable
{
	private readonly string _baseFilePath;
	private readonly long _maxFileSizeBytes;
	private readonly object _writeLock = new();
	private readonly JsonSerializerOptions _jsonOptions;

	private StreamWriter? _currentWriter;
	private string? _currentFilePath;
	private int _rotationCounter = 0;
	private long _currentFileSize = 0;
	private bool _disposed;

	/// <summary>
	/// Creates a new JSONL file writer with automatic rotation.
	/// </summary>
	/// <param name="baseFilePath">Base path for output files (e.g., "debug/run.jsonl")</param>
	/// <param name="maxFileSizeMB">Maximum file size in megabytes before rotation (default: 10MB)</param>
	public FileBasedWriter(string baseFilePath, int maxFileSizeMB = 10)
	{
		if (string.IsNullOrWhiteSpace(baseFilePath))
			throw new ArgumentException("Base file path cannot be null or empty", nameof(baseFilePath));

		if (maxFileSizeMB <= 0)
			throw new ArgumentException("Max file size must be positive", nameof(maxFileSizeMB));

		_baseFilePath = baseFilePath;
		_maxFileSizeBytes = maxFileSizeMB * 1024L * 1024L;

		// JSON serialization options
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			WriteIndented = false // Compact format for JSONL
		};

		// Ensure directory exists
		var directory = Path.GetDirectoryName(_baseFilePath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		// Initialize first file
		InitializeWriter();
	}

	/// <summary>
	/// Current file path being written to.
	/// </summary>
	public string CurrentFilePath
	{
		get
		{
			lock (_writeLock)
			{
				return _currentFilePath ?? _baseFilePath;
			}
		}
	}

	/// <summary>
	/// Total number of files created (including rotations).
	/// </summary>
	public int FileCount => _rotationCounter + 1;

	/// <summary>
	/// Writes a single debug event to the JSONL file.
	/// </summary>
	/// <param name="eventType">Type of event (e.g., "candle", "trade", "indicator_SMA_20")</param>
	/// <param name="eventData">Event data object to serialize</param>
	public void WriteEvent(string eventType, object eventData)
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(FileBasedWriter));

		if (string.IsNullOrEmpty(eventType))
			throw new ArgumentException("Event type cannot be null or empty", nameof(eventType));

		if (eventData == null)
			throw new ArgumentNullException(nameof(eventData));

		lock (_writeLock)
		{
			CheckAndRotateFile();

			// Create JSONL line: {"type":"candle","data":{...}}
			var line = new
			{
				type = eventType,
				data = eventData
			};

			var json = JsonSerializer.Serialize(line, _jsonOptions);
			_currentWriter!.WriteLine(json);

			// Update file size tracking
			_currentFileSize += Encoding.UTF8.GetByteCount(json) + Environment.NewLine.Length;

			// Flush to ensure browser can read immediately
			_currentWriter.Flush();
		}
	}

	/// <summary>
	/// Writes a batch of debug events from the buffer.
	/// Events are grouped by type in the dictionary.
	/// </summary>
	/// <param name="events">Dictionary of event type to list of event data</param>
	public void WriteBatch(Dictionary<string, List<object>> events)
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(FileBasedWriter));

		if (events == null || events.Count == 0)
			return;

		lock (_writeLock)
		{
			foreach (var (eventType, eventList) in events)
			{
				foreach (var eventData in eventList)
				{
					CheckAndRotateFile();

					// Create JSONL line
					var line = new
					{
						type = eventType,
						data = eventData
					};

					var json = JsonSerializer.Serialize(line, _jsonOptions);
					_currentWriter!.WriteLine(json);

					// Update file size tracking
					_currentFileSize += Encoding.UTF8.GetByteCount(json) + Environment.NewLine.Length;
				}
			}

			// Flush after batch to ensure all events are visible
			_currentWriter!.Flush();
		}
	}

	/// <summary>
	/// Checks if file size exceeds limit and rotates to new file if needed.
	/// Must be called within _writeLock.
	/// </summary>
	private void CheckAndRotateFile()
	{
		if (_currentFileSize < _maxFileSizeBytes)
			return;

		// Close current file
		_currentWriter?.Dispose();

		// Increment rotation counter
		_rotationCounter++;

		// Create new file with rotation suffix
		InitializeWriter();
	}

	/// <summary>
	/// Initializes a new StreamWriter for the current rotation.
	/// Must be called within _writeLock.
	/// </summary>
	private void InitializeWriter()
	{
		// Generate file path with rotation suffix if needed
		if (_rotationCounter == 0)
		{
			_currentFilePath = _baseFilePath;
		}
		else
		{
			// Insert rotation suffix before extension
			var directory = Path.GetDirectoryName(_baseFilePath) ?? "";
			var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_baseFilePath);
			var extension = Path.GetExtension(_baseFilePath);

			_currentFilePath = Path.Combine(directory,
				$"{fileNameWithoutExt}_{_rotationCounter:D3}{extension}");
		}

		// Create new StreamWriter with UTF-8 encoding (no BOM)
		var fileStream = new FileStream(
			_currentFilePath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.Read, // Allow browser to read while writing
			bufferSize: 4096,
			useAsync: false);

		_currentWriter = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
		{
			AutoFlush = false // We'll flush manually after each batch
		};

		_currentFileSize = 0;
	}

	/// <summary>
	/// Disposes the writer and closes the current file.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;

		lock (_writeLock)
		{
			_currentWriter?.Flush();
			_currentWriter?.Dispose();
			_currentWriter = null;
		}
	}
}
