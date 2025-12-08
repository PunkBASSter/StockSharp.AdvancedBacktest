using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;

public sealed class SqliteEventSink : IEventSink
{
	private readonly string _databasePath;
	private SqliteConnection? _connection;
	private BatchEventWriter? _writer;
	private bool _disposed;

	public SqliteEventSink(string databasePath)
	{
		if (string.IsNullOrWhiteSpace(databasePath))
			throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));

		_databasePath = databasePath;
	}

	public string DatabasePath => _databasePath;

	public async Task InitializeAsync(string runId)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_connection != null)
			throw new InvalidOperationException("SqliteEventSink has already been initialized");

		var directory = Path.GetDirectoryName(_databasePath);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		_connection = new SqliteConnection($"Data Source={_databasePath}");
		await _connection.OpenAsync();

		await DatabaseSchema.InitializeAsync(_connection);

		var repository = new SqliteEventRepository(_connection);
		_writer = new BatchEventWriter(repository, batchSize: 500, flushInterval: TimeSpan.FromMilliseconds(500));

		var run = new BacktestRunEntity
		{
			Id = runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow,
			StrategyConfigHash = Guid.NewGuid().ToString("N")
		};

		await repository.CreateBacktestRunAsync(run);
	}

	public async Task WriteEventAsync(EventEntity entity)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_writer == null)
			throw new InvalidOperationException("SqliteEventSink has not been initialized. Call InitializeAsync first.");

		await _writer.WriteEventAsync(entity);
	}

	public async Task FlushAsync()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_writer != null)
		{
			await _writer.FlushAsync();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;

		if (_writer != null)
		{
			await _writer.DisposeAsync();
			_writer = null;
		}

		if (_connection != null)
		{
			await _connection.DisposeAsync();
			_connection = null;
		}

		_disposed = true;
	}
}
