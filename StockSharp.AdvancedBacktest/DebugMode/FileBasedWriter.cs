using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace StockSharp.AdvancedBacktest.DebugMode;

/// <summary>
/// Writes debug events to JSONL (JSON Lines) files.
/// Thread-safe for concurrent writes from event buffer.
/// JSONL format allows browser to read file while it's being written.
/// </summary>
public class FileBasedWriter : IDisposable
{
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly JsonSerializerOptions _jsonOptions;

    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>
    /// Creates a new JSONL file writer.
    /// </summary>
    /// <param name="filePath">Path for output file (e.g., "debug/latest.jsonl")</param>
    public FileBasedWriter(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        _filePath = filePath;

        // JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false // Compact format for JSONL
        };

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Initialize writer
        InitializeWriter();
    }

    /// <summary>
    /// Current file path being written to.
    /// </summary>
    public string CurrentFilePath => _filePath;

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
            // Create JSONL line: {"type":"candle","data":{...}}
            var line = new
            {
                type = eventType,
                data = eventData
            };

            var json = JsonSerializer.Serialize(line, _jsonOptions);
            _writer!.WriteLine(json);

            // Flush to ensure browser can read immediately
            _writer.Flush();
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
                    // Create JSONL line
                    var line = new
                    {
                        type = eventType,
                        data = eventData
                    };

                    var json = JsonSerializer.Serialize(line, _jsonOptions);
                    _writer!.WriteLine(json);
                }
            }

            // Flush after batch to ensure all events are visible
            _writer!.Flush();
        }
    }

    /// <summary>
    /// Initializes the StreamWriter.
    /// Must be called within _writeLock or during construction.
    /// </summary>
    private void InitializeWriter()
    {
        // Create new StreamWriter with UTF-8 encoding (no BOM)
        var fileStream = new FileStream(
            _filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read, // Allow browser to read while writing
            bufferSize: 4096,
            useAsync: false);

        _writer = new StreamWriter(fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false // We'll flush manually after each batch
        };
    }

    /// <summary>
    /// Disposes the writer and closes the file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_writeLock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
