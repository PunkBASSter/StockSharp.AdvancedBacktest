using System.Collections.Concurrent;
using StockSharp.Algo.Storages;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Storages;

/// <summary>
/// A thread-safe wrapper for <see cref="IMarketDataStorage{T}"/> that caches loaded data
/// and shares it across multiple consumers. This avoids the await using disposal bug
/// in BasketMarketDataStorage and provides significant I/O reduction during optimization.
/// </summary>
public sealed class SharedMarketDataStorage<T> : IMarketDataStorage<T>
    where T : Message
{
    private readonly IMarketDataStorage<T> _inner;
    private readonly ConcurrentDictionary<DateTime, T[]> _cache = new();
    private readonly ConcurrentDictionary<DateTime, SemaphoreSlim> _loadLocks = new();

    public SharedMarketDataStorage(IMarketDataStorage<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public DataType DataType => _inner.DataType;
    public SecurityId SecurityId => _inner.SecurityId;
    public IMarketDataStorageDrive Drive => _inner.Drive;
    public bool AppendOnlyNew
    {
        get => _inner.AppendOnlyNew;
        set => _inner.AppendOnlyNew = value;
    }
    public IMarketDataSerializer<T> Serializer => _inner.Serializer;
    IMarketDataSerializer IMarketDataStorage.Serializer => _inner.Serializer;

    public IAsyncEnumerable<T> LoadAsync(DateTime date, CancellationToken cancellationToken)
    {
        return new DisposalSafeAsyncEnumerable<T>(() => LoadDataAsync(date, cancellationToken));
    }

    private async Task<T[]> LoadDataAsync(DateTime date, CancellationToken cancellationToken)
    {
        // Fast path: data already cached
        if (_cache.TryGetValue(date, out var cached))
            return cached;

        // Get or create lock for this specific date
        var loadLock = _loadLocks.GetOrAdd(date, _ => new SemaphoreSlim(1, 1));

        await loadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(date, out cached))
                return cached;

            // Load and materialize data from inner storage
            var list = new List<T>();
            await foreach (var msg in _inner.LoadAsync(date, cancellationToken))
            {
                list.Add(msg);
            }

            cached = list.ToArray();
            _cache.TryAdd(date, cached);
            return cached;
        }
        finally
        {
            loadLock.Release();
        }
    }

    IAsyncEnumerable<Message> IMarketDataStorage.LoadAsync(DateTime date, CancellationToken cancellationToken)
        => new DisposalSafeAsyncEnumerable<Message>(() => LoadDataAsync(date, cancellationToken)
            .ContinueWith(t => t.Result.Cast<Message>().ToArray(), cancellationToken));

    public ValueTask<IEnumerable<DateTime>> GetDatesAsync(CancellationToken cancellationToken)
        => _inner.GetDatesAsync(cancellationToken);

    public ValueTask<IMarketDataMetaInfo> GetMetaInfoAsync(DateTime date, CancellationToken cancellationToken)
        => _inner.GetMetaInfoAsync(date, cancellationToken);

    public ValueTask<int> SaveAsync(IEnumerable<T> data, CancellationToken cancellationToken)
        => _inner.SaveAsync(data, cancellationToken);

    ValueTask<int> IMarketDataStorage.SaveAsync(IEnumerable<Message> data, CancellationToken cancellationToken)
        => _inner.SaveAsync(data.Cast<T>(), cancellationToken);

    public ValueTask DeleteAsync(IEnumerable<T> data, CancellationToken cancellationToken)
        => _inner.DeleteAsync(data, cancellationToken);

    ValueTask IMarketDataStorage.DeleteAsync(IEnumerable<Message> data, CancellationToken cancellationToken)
        => _inner.DeleteAsync(data.Cast<T>(), cancellationToken);

    public ValueTask DeleteAsync(DateTime date, CancellationToken cancellationToken)
    {
        _cache.TryRemove(date, out _);
        return _inner.DeleteAsync(date, cancellationToken);
    }

    public void ClearCache() => _cache.Clear();

    public int CachedDateCount => _cache.Count;
}

/// <summary>
/// An async enumerable that creates disposal-safe enumerators.
/// The enumerator's Current property remains valid even after DisposeAsync() is called,
/// which is required to work around the await using bug in BasketMarketDataStorage.
/// </summary>
internal sealed class DisposalSafeAsyncEnumerable<T> : IAsyncEnumerable<T>
{
    private readonly Func<Task<T[]>> _dataLoader;

    public DisposalSafeAsyncEnumerable(Func<Task<T[]>> dataLoader)
    {
        _dataLoader = dataLoader ?? throw new ArgumentNullException(nameof(dataLoader));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new DisposalSafeAsyncEnumerator<T>(_dataLoader, cancellationToken);
    }
}

/// <summary>
/// An async enumerator where Current remains valid after disposal.
/// BasketMarketDataStorage uses "await using" which disposes the enumerator
/// before it's fully consumed from the priority queue. This enumerator
/// keeps Current valid after disposal to work around that bug.
/// </summary>
internal sealed class DisposalSafeAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly Func<Task<T[]>> _dataLoader;
    private readonly CancellationToken _cancellationToken;
    private T[]? _data;
    private int _index = -1;
    private T? _current;

    public DisposalSafeAsyncEnumerator(Func<Task<T[]>> dataLoader, CancellationToken cancellationToken)
    {
        _dataLoader = dataLoader;
        _cancellationToken = cancellationToken;
    }

    // KEY FIX: Current stays valid even after disposal
    public T Current => _current!;

    public async ValueTask<bool> MoveNextAsync()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Lazy load data on first MoveNext
        if (_data is null)
        {
            _data = await _dataLoader();
        }

        _index++;
        if (_index < _data.Length)
        {
            _current = _data[_index];
            return true;
        }

        return false;
    }

    // DisposeAsync is a no-op - we keep _current valid intentionally
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
