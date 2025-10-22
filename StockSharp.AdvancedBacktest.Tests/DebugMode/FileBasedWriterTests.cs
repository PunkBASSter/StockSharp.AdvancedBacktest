using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.Export;

namespace StockSharp.AdvancedBacktest.Tests.DebugMode;

public class FileBasedWriterTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly List<string> _filesToCleanup = new();

	public FileBasedWriterTests()
	{
		// Create unique test directory for each test run
		_testDirectory = Path.Combine(Path.GetTempPath(), $"DebugModeTests_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_testDirectory);
	}

	public void Dispose()
	{
		// Cleanup test files
		foreach (var file in _filesToCleanup)
		{
			try
			{
				if (File.Exists(file))
					File.Delete(file);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}

		try
		{
			if (Directory.Exists(_testDirectory))
				Directory.Delete(_testDirectory, recursive: true);
		}
		catch
		{
			// Ignore cleanup errors
		}
	}

	private string GetTestFilePath(string filename = "test.jsonl")
	{
		var path = Path.Combine(_testDirectory, filename);
		_filesToCleanup.Add(path);
		return path;
	}

	#region JSONL Format Tests

	[Fact]
	public void WriteEvent_CreatesValidJSONL_SingleLine()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		var eventData = new { Time = 1729555200, Value = 42.5 };

		// Act
		writer.WriteEvent("test", eventData);
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Single(lines);

		var json = JsonDocument.Parse(lines[0]);
		Assert.Equal("test", json.RootElement.GetProperty("type").GetString());
		Assert.Equal(1729555200, json.RootElement.GetProperty("data").GetProperty("time").GetInt64());
		Assert.Equal(42.5, json.RootElement.GetProperty("data").GetProperty("value").GetDouble());
	}

	[Fact]
	public void WriteEvent_MultipleEvents_EachLineValidJSON()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act
		for (int i = 0; i < 10; i++)
		{
			writer.WriteEvent("test", new { Index = i, Value = i * 10 });
		}
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Equal(10, lines.Length);

		for (int i = 0; i < 10; i++)
		{
			var json = JsonDocument.Parse(lines[i]);
			Assert.Equal("test", json.RootElement.GetProperty("type").GetString());
			Assert.Equal(i, json.RootElement.GetProperty("data").GetProperty("index").GetInt32());
		}
	}

	[Fact]
	public void WriteEvent_UsesUTF8Encoding_NoBOM()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		writer.WriteEvent("test", new { Message = "Hello 世界" });
		writer.Dispose();

		// Assert
		var bytes = File.ReadAllBytes(filePath);

		// Check no BOM (UTF-8 BOM is EF BB BF)
		Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
			"File should not contain UTF-8 BOM");

		// Verify content is valid UTF-8 and contains the message (may be Unicode-escaped in JSON)
		var content = File.ReadAllText(filePath, Encoding.UTF8);
		var json = JsonDocument.Parse(content.Trim());
		var message = json.RootElement.GetProperty("data").GetProperty("message").GetString();
		Assert.Equal("Hello 世界", message);
	}

	[Fact]
	public void WriteBatch_CreatesValidJSONL_MultipleTypes()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		var batch = new Dictionary<string, List<object>>
		{
			["candle"] = new List<object>
			{
				new CandleDataPoint { Time = 1, Open = 100, High = 105, Low = 99, Close = 103, Volume = 1000 },
				new CandleDataPoint { Time = 2, Open = 103, High = 107, Low = 102, Close = 106, Volume = 1200 }
			},
			["trade"] = new List<object>
			{
				new TradeDataPoint { Time = 1, Price = 103, Volume = 10, Side = "buy", PnL = 0 }
			}
		};

		// Act
		writer.WriteBatch(batch);
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Equal(3, lines.Length); // 2 candles + 1 trade

		// Verify first two lines are candles
		var candle1 = JsonDocument.Parse(lines[0]);
		Assert.Equal("candle", candle1.RootElement.GetProperty("type").GetString());

		var candle2 = JsonDocument.Parse(lines[1]);
		Assert.Equal("candle", candle2.RootElement.GetProperty("type").GetString());

		// Verify last line is trade
		var trade = JsonDocument.Parse(lines[2]);
		Assert.Equal("trade", trade.RootElement.GetProperty("type").GetString());
	}

	#endregion

	#region File Rotation Tests

	[Fact]
	public void WriteEvent_ExceedsMaxSize_CreatesRotatedFile()
	{
		// Arrange - Use very small max size (1KB) to trigger rotation
		var filePath = GetTestFilePath();
		var rotatedPath1 = Path.Combine(_testDirectory, "test_001.jsonl");
		_filesToCleanup.Add(rotatedPath1);

		using var writer = new FileBasedWriter(filePath, maxFileSizeMB: 1);

		// Create large event data to exceed 1MB quickly
		var largeData = new string('x', 10000); // 10KB per event

		// Act - Write enough events to exceed 1MB
		for (int i = 0; i < 110; i++) // 110 * 10KB > 1MB
		{
			writer.WriteEvent("test", new { Index = i, Data = largeData });
		}

		// Assert
		Assert.True(File.Exists(filePath), "Base file should exist");
		Assert.True(File.Exists(rotatedPath1), "Rotated file should be created");
		Assert.Equal(2, writer.FileCount);
	}

	[Fact]
	public void WriteEvent_MultipleRotations_CreatesSequentialFiles()
	{
		// Arrange
		var filePath = GetTestFilePath();
		var rotatedPath1 = Path.Combine(_testDirectory, "test_001.jsonl");
		var rotatedPath2 = Path.Combine(_testDirectory, "test_002.jsonl");
		_filesToCleanup.Add(rotatedPath1);
		_filesToCleanup.Add(rotatedPath2);

		using var writer = new FileBasedWriter(filePath, maxFileSizeMB: 1);

		var largeData = new string('x', 10000);

		// Act - Write enough to create 3 files
		for (int i = 0; i < 220; i++) // Should create base + _001 + _002
		{
			writer.WriteEvent("test", new { Index = i, Data = largeData });
		}

		// Assert
		Assert.True(File.Exists(filePath));
		Assert.True(File.Exists(rotatedPath1));
		Assert.True(File.Exists(rotatedPath2));
		Assert.Equal(3, writer.FileCount);
	}

	[Fact]
	public void CurrentFilePath_AfterRotation_ReturnsNewPath()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath, maxFileSizeMB: 1);

		var initialPath = writer.CurrentFilePath;
		var largeData = new string('x', 10000);

		// Act - Trigger rotation
		for (int i = 0; i < 110; i++)
		{
			writer.WriteEvent("test", new { Index = i, Data = largeData });
		}

		// Assert
		Assert.NotEqual(initialPath, writer.CurrentFilePath);
		Assert.Contains("_001", writer.CurrentFilePath);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task WriteEvent_ConcurrentWrites_NoCorruptedJSON()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		const int threadCount = 10;
		const int eventsPerThread = 50;

		// Act - Multiple threads writing concurrently
		var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
			Task.Run(() =>
			{
				for (int i = 0; i < eventsPerThread; i++)
				{
					writer.WriteEvent($"thread_{threadId}", new { ThreadId = threadId, Index = i });
				}
			})
		).ToArray();

		await Task.WhenAll(tasks);
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Equal(threadCount * eventsPerThread, lines.Length);

		// Verify each line is valid JSON
		foreach (var line in lines)
		{
			var json = JsonDocument.Parse(line); // Will throw if invalid
			Assert.NotNull(json);
		}
	}

	[Fact]
	public async Task WriteBatch_ConcurrentBatches_AllEventsWritten()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		const int batchCount = 20;
		const int eventsPerBatch = 10;

		// Act
		var tasks = Enumerable.Range(0, batchCount).Select(batchId =>
			Task.Run(() =>
			{
				var batch = new Dictionary<string, List<object>>
				{
					[$"batch_{batchId}"] = Enumerable.Range(0, eventsPerBatch)
						.Select(i => (object)new { BatchId = batchId, Index = i })
						.ToList()
				};

				writer.WriteBatch(batch);
			})
		).ToArray();

		await Task.WhenAll(tasks);
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Equal(batchCount * eventsPerBatch, lines.Length);
	}

	#endregion

	#region Batch Writing Tests

	[Fact]
	public void WriteBatch_EmptyBatch_DoesNothing()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act
		writer.WriteBatch(new Dictionary<string, List<object>>());
		writer.Dispose();

		// Assert
		Assert.True(File.Exists(filePath));
		var lines = File.ReadAllLines(filePath);
		Assert.Empty(lines);
	}

	[Fact]
	public void WriteBatch_NullBatch_DoesNothing()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act
		writer.WriteBatch(null!);
		writer.Dispose();

		// Assert
		var lines = File.ReadAllLines(filePath);
		Assert.Empty(lines);
	}

	#endregion

	#region Disposal Tests

	[Fact]
	public void Dispose_ClosesFile_CanReadImmediately()
	{
		// Arrange
		var filePath = GetTestFilePath();
		var writer = new FileBasedWriter(filePath);

		writer.WriteEvent("test", new { Value = 1 });

		// Act
		writer.Dispose();

		// Assert - Should be able to read immediately after disposal
		var content = File.ReadAllText(filePath);
		Assert.NotEmpty(content);
	}

	[Fact]
	public void Dispose_MultipleCalls_NoException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		var writer = new FileBasedWriter(filePath);

		// Act & Assert
		writer.Dispose();
		writer.Dispose();
		writer.Dispose();
	}

	[Fact]
	public void WriteEvent_AfterDisposal_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		var writer = new FileBasedWriter(filePath);
		writer.Dispose();

		// Act & Assert
		Assert.Throws<ObjectDisposedException>(() =>
			writer.WriteEvent("test", new { Value = 1 }));
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_NullFilePath_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new FileBasedWriter(null!));
	}

	[Fact]
	public void Constructor_EmptyFilePath_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new FileBasedWriter(""));
	}

	[Fact]
	public void Constructor_NegativeMaxSize_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			new FileBasedWriter(GetTestFilePath(), maxFileSizeMB: -1));
	}

	[Fact]
	public void Constructor_ZeroMaxSize_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			new FileBasedWriter(GetTestFilePath(), maxFileSizeMB: 0));
	}

	[Fact]
	public void Constructor_CreatesDirectory_IfNotExists()
	{
		// Arrange
		var subDir = Path.Combine(_testDirectory, "subdir", "nested");
		var filePath = Path.Combine(subDir, "test.jsonl");
		_filesToCleanup.Add(filePath);

		// Act
		using var writer = new FileBasedWriter(filePath);

		// Assert
		Assert.True(Directory.Exists(subDir));
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void WriteEvent_NullEventType_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			writer.WriteEvent(null!, new { Value = 1 }));
	}

	[Fact]
	public void WriteEvent_EmptyEventType_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act & Assert
		Assert.Throws<ArgumentException>(() =>
			writer.WriteEvent("", new { Value = 1 }));
	}

	[Fact]
	public void WriteEvent_NullEventData_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() =>
			writer.WriteEvent("test", null!));
	}

	#endregion

	#region Performance Tests

	[Fact]
	public void WriteBatch_100Events_CompletesQuickly()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var writer = new FileBasedWriter(filePath);

		var batch = new Dictionary<string, List<object>>
		{
			["test"] = Enumerable.Range(0, 100)
				.Select(i => (object)new { Index = i, Value = i * 10 })
				.ToList()
		};

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		writer.WriteBatch(batch);
		stopwatch.Stop();

		// Assert - Target: <5ms for 100 events (but allow up to 50ms on slow systems)
		Assert.True(stopwatch.ElapsedMilliseconds < 50,
			$"WriteBatch took {stopwatch.ElapsedMilliseconds}ms (target: <50ms)");
	}

	#endregion
}
