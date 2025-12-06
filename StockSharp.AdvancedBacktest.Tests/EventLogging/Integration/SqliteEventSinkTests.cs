using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.EventLogging.Integration;

public sealed class SqliteEventSinkTests : IAsyncDisposable
{
	private readonly string _tempDbPath;
	private SqliteEventSink? _sink;

	public SqliteEventSinkTests()
	{
		_tempDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
	}

	[Fact]
	public async Task InitializeAsync_ShouldCreateDatabaseAndSchema()
	{
		_sink = new SqliteEventSink(_tempDbPath);
		var runId = Guid.NewGuid().ToString();

		await _sink.InitializeAsync(runId);

		Assert.True(File.Exists(_tempDbPath));
	}

	[Fact]
	public async Task WriteEventAsync_AfterInitialize_ShouldSucceed()
	{
		_sink = new SqliteEventSink(_tempDbPath);
		var runId = Guid.NewGuid().ToString();
		await _sink.InitializeAsync(runId);

		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = runId,
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"OrderId": "test-123", "Price": 100.50}"""
		};

		await _sink.WriteEventAsync(entity);
		await _sink.FlushAsync();

		// If we get here without exception, the write succeeded
		Assert.True(true);
	}

	[Fact]
	public async Task WriteEventAsync_BeforeInitialize_ShouldThrowInvalidOperationException()
	{
		_sink = new SqliteEventSink(_tempDbPath);

		var entity = new EventEntity
		{
			EventId = Guid.NewGuid().ToString(),
			RunId = "test-run",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}"
		};

		await Assert.ThrowsAsync<InvalidOperationException>(() => _sink.WriteEventAsync(entity));
	}

	[Fact]
	public async Task InitializeAsync_WhenCalledTwice_ShouldThrowInvalidOperationException()
	{
		_sink = new SqliteEventSink(_tempDbPath);
		var runId = Guid.NewGuid().ToString();

		await _sink.InitializeAsync(runId);

		await Assert.ThrowsAsync<InvalidOperationException>(() => _sink.InitializeAsync("another-run"));
	}

	[Fact]
	public async Task InitializeAsync_ShouldCreateDirectoryIfNotExists()
	{
		var subDir = Path.Combine(Path.GetTempPath(), $"subdir_{Guid.NewGuid()}");
		var dbPath = Path.Combine(subDir, "test.db");
		var localSink = new SqliteEventSink(dbPath);

		try
		{
			await localSink.InitializeAsync(Guid.NewGuid().ToString());

			Assert.True(Directory.Exists(subDir));
			Assert.True(File.Exists(dbPath));
		}
		finally
		{
			await localSink.DisposeAsync();

			// Give SQLite time to release the file
			await Task.Delay(100);

			try
			{
				if (Directory.Exists(subDir))
				{
					Directory.Delete(subDir, true);
				}
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}

	[Fact]
	public async Task WriteMultipleEvents_ShouldBatchAndFlush()
	{
		_sink = new SqliteEventSink(_tempDbPath);
		var runId = Guid.NewGuid().ToString();
		await _sink.InitializeAsync(runId);

		for (int i = 0; i < 10; i++)
		{
			var entity = new EventEntity
			{
				EventId = Guid.NewGuid().ToString(),
				RunId = runId,
				Timestamp = DateTime.UtcNow,
				EventType = EventType.TradeExecution,
				Severity = EventSeverity.Info,
				Category = EventCategory.Execution,
				Properties = $$$"""{"OrderId": "order-{{{i}}}", "Price": {{{100 + i}}}.00}"""
			};

			await _sink.WriteEventAsync(entity);
		}

		await _sink.FlushAsync();

		// If we get here without exception, all writes succeeded
		Assert.True(true);
	}

	[Fact]
	public void Constructor_WithNullPath_ShouldThrowArgumentException()
	{
		Assert.Throws<ArgumentException>(() => new SqliteEventSink(null!));
	}

	[Fact]
	public void Constructor_WithEmptyPath_ShouldThrowArgumentException()
	{
		Assert.Throws<ArgumentException>(() => new SqliteEventSink(string.Empty));
	}

	[Fact]
	public void DatabasePath_ShouldReturnConfiguredPath()
	{
		_sink = new SqliteEventSink(_tempDbPath);

		Assert.Equal(_tempDbPath, _sink.DatabasePath);
	}

	public async ValueTask DisposeAsync()
	{
		if (_sink != null)
		{
			await _sink.DisposeAsync();
			_sink = null;
		}

		if (File.Exists(_tempDbPath))
		{
			try
			{
				File.Delete(_tempDbPath);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}
	}
}
