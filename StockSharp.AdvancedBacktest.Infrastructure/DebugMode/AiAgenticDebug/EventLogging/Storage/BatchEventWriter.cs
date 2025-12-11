using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;

public sealed class BatchEventWriter : IAsyncDisposable
{
	private readonly IEventRepository _repository;
	private readonly List<EventEntity> _buffer = new();
	private readonly int _batchSize;
	private readonly Timer _flushTimer;
	private readonly SemaphoreSlim _lock = new(1, 1);
	private bool _disposed;

	public BatchEventWriter(IEventRepository repository, int batchSize = 1000, TimeSpan? flushInterval = null)
	{
		_repository = repository;
		_batchSize = batchSize;
		_flushTimer = new Timer(_ => FlushAsync().Wait(), null, flushInterval ?? TimeSpan.FromSeconds(30), flushInterval ?? TimeSpan.FromSeconds(30));
	}

	public async Task WriteEventAsync(EventEntity eventEntity)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await _lock.WaitAsync();
		try
		{
			_buffer.Add(eventEntity);
		}
		finally
		{
			_lock.Release();
		}

		if (_buffer.Count >= _batchSize)
		{
			await FlushAsync();
		}
	}

	public async Task FlushAsync()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		List<EventEntity> eventsToWrite;

		await _lock.WaitAsync();
		try
		{
			if (_buffer.Count == 0) return;
			eventsToWrite = new List<EventEntity>(_buffer);
			_buffer.Clear();
		}
		finally
		{
			_lock.Release();
		}

		foreach (var evt in eventsToWrite)
		{
			await _repository.WriteEventAsync(evt);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;

		await _flushTimer.DisposeAsync();
		await FlushAsync();

		_lock.Dispose();
		_disposed = true;
	}
}
