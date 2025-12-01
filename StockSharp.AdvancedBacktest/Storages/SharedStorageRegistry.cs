using System.Collections.Concurrent;
using StockSharp.Algo.Storages;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Storages;

/// <summary>
/// A wrapper for <see cref="IStorageRegistry"/> that provides shared, thread-safe caching
/// of market data across multiple consumers. This is particularly useful during optimization
/// where multiple parallel strategy runs access the same historical data.
/// </summary>
/// <remarks>
/// Key features:
/// - All storage instances are cached and reused across calls
/// - Data loaded once is shared across all parallel runs
/// - Materializes data before returning, avoiding the await using disposal bug
/// - Thread-safe for concurrent access
/// </remarks>
public sealed class SharedStorageRegistry : IStorageRegistry
{
    private readonly IStorageRegistry _inner;

    // Cache wrapped storages to ensure same data is shared
    private readonly ConcurrentDictionary<StorageKey, object> _storageCache = new();

    private record struct StorageKey(SecurityId SecurityId, DataType? DataType, StorageFormats Format, bool PassThrough = false);

    public SharedStorageRegistry(IStorageRegistry inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IMarketDataDrive DefaultDrive
    {
        get => _inner.DefaultDrive;
        set => _inner.DefaultDrive = value;
    }

    public IExchangeInfoProvider ExchangeInfoProvider => _inner.ExchangeInfoProvider;

    public IMarketDataStorage<NewsMessage> GetNewsMessageStorage(
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(default, DataType.News, format);
        return (IMarketDataStorage<NewsMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<NewsMessage>(_inner.GetNewsMessageStorage(drive, format)));
    }

    public IMarketDataStorage<BoardStateMessage> GetBoardStateMessageStorage(
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(default, DataType.BoardState, format);
        return (IMarketDataStorage<BoardStateMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<BoardStateMessage>(_inner.GetBoardStateMessageStorage(drive, format)));
    }

    public IMarketDataStorage<ExecutionMessage> GetTickMessageStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, DataType.Ticks, format);
        return (IMarketDataStorage<ExecutionMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<ExecutionMessage>(_inner.GetTickMessageStorage(securityId, drive, format)));
    }

    public IMarketDataStorage<QuoteChangeMessage> GetQuoteMessageStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary,
        bool passThroughOrderBookIncrement = false)
    {
        var key = new StorageKey(securityId, DataType.MarketDepth, format, passThroughOrderBookIncrement);
        return (IMarketDataStorage<QuoteChangeMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<QuoteChangeMessage>(
                _inner.GetQuoteMessageStorage(securityId, drive, format, passThroughOrderBookIncrement)));
    }

    public IMarketDataStorage<ExecutionMessage> GetOrderLogMessageStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, DataType.OrderLog, format);
        return (IMarketDataStorage<ExecutionMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<ExecutionMessage>(_inner.GetOrderLogMessageStorage(securityId, drive, format)));
    }

    public IMarketDataStorage<Level1ChangeMessage> GetLevel1MessageStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, DataType.Level1, format);
        return (IMarketDataStorage<Level1ChangeMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<Level1ChangeMessage>(_inner.GetLevel1MessageStorage(securityId, drive, format)));
    }

    public IMarketDataStorage<PositionChangeMessage> GetPositionMessageStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, DataType.PositionChanges, format);
        return (IMarketDataStorage<PositionChangeMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<PositionChangeMessage>(_inner.GetPositionMessageStorage(securityId, drive, format)));
    }

    public IMarketDataStorage<CandleMessage> GetCandleMessageStorage(
        SecurityId securityId,
        DataType type,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, type, format);
        return (IMarketDataStorage<CandleMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<CandleMessage>(_inner.GetCandleMessageStorage(securityId, type, drive, format)));
    }

    public IMarketDataStorage<ExecutionMessage> GetExecutionMessageStorage(
        SecurityId securityId,
        DataType type,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, type, format);
        return (IMarketDataStorage<ExecutionMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<ExecutionMessage>(_inner.GetExecutionMessageStorage(securityId, type, drive, format)));
    }

    public IMarketDataStorage<ExecutionMessage> GetTransactionStorage(
        SecurityId securityId,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        var key = new StorageKey(securityId, DataType.Transactions, format);
        return (IMarketDataStorage<ExecutionMessage>)_storageCache.GetOrAdd(key, _ =>
            new SharedMarketDataStorage<ExecutionMessage>(_inner.GetTransactionStorage(securityId, drive, format)));
    }

    public IMarketDataStorage GetStorage(
        SecurityId securityId,
        DataType dataType,
        IMarketDataDrive? drive = null,
        StorageFormats format = StorageFormats.Binary)
    {
        // Route to specific typed methods for proper caching
        if (dataType == DataType.Ticks)
            return GetTickMessageStorage(securityId, drive, format);
        if (dataType == DataType.MarketDepth)
            return GetQuoteMessageStorage(securityId, drive, format);
        if (dataType == DataType.OrderLog)
            return GetOrderLogMessageStorage(securityId, drive, format);
        if (dataType == DataType.Level1)
            return GetLevel1MessageStorage(securityId, drive, format);
        if (dataType == DataType.PositionChanges)
            return GetPositionMessageStorage(securityId, drive, format);
        if (dataType == DataType.Transactions)
            return GetTransactionStorage(securityId, drive, format);
        if (dataType == DataType.News)
            return GetNewsMessageStorage(drive, format);
        if (dataType == DataType.BoardState)
            return GetBoardStateMessageStorage(drive, format);
        if (dataType.IsCandles)
            return GetCandleMessageStorage(securityId, dataType, drive, format);

        // Fallback for other types - wrap generically
        var key = new StorageKey(securityId, dataType, format);
        return (IMarketDataStorage)_storageCache.GetOrAdd(key, _ =>
            WrapGenericStorage(_inner.GetStorage(securityId, dataType, drive, format)));
    }

    private static IMarketDataStorage WrapGenericStorage(IMarketDataStorage storage)
    {
        // For generic storage, we can't easily wrap with SharedMarketDataStorage<T>
        // because we don't know T at compile time. Return unwrapped as fallback.
        // Most commonly used types are handled by specific methods above.
        return storage;
    }

    // Register methods delegate to inner registry
    public void RegisterTradeStorage(IMarketDataStorage<ExecutionMessage> storage)
        => _inner.RegisterTradeStorage(storage);

    public void RegisterMarketDepthStorage(IMarketDataStorage<QuoteChangeMessage> storage)
        => _inner.RegisterMarketDepthStorage(storage);

    public void RegisterOrderLogStorage(IMarketDataStorage<ExecutionMessage> storage)
        => _inner.RegisterOrderLogStorage(storage);

    public void RegisterLevel1Storage(IMarketDataStorage<Level1ChangeMessage> storage)
        => _inner.RegisterLevel1Storage(storage);

    public void RegisterPositionStorage(IMarketDataStorage<PositionChangeMessage> storage)
        => _inner.RegisterPositionStorage(storage);

    public void RegisterCandleStorage(IMarketDataStorage<CandleMessage> storage)
        => _inner.RegisterCandleStorage(storage);

    /// <summary>
    /// Clears all cached data across all storages.
    /// </summary>
    public void ClearAllCaches()
    {
        foreach (var storage in _storageCache.Values)
        {
            if (storage is SharedMarketDataStorage<CandleMessage> candleStorage)
                candleStorage.ClearCache();
            else if (storage is SharedMarketDataStorage<ExecutionMessage> execStorage)
                execStorage.ClearCache();
            else if (storage is SharedMarketDataStorage<QuoteChangeMessage> quoteStorage)
                quoteStorage.ClearCache();
            else if (storage is SharedMarketDataStorage<Level1ChangeMessage> level1Storage)
                level1Storage.ClearCache();
            else if (storage is SharedMarketDataStorage<PositionChangeMessage> posStorage)
                posStorage.ClearCache();
            else if (storage is SharedMarketDataStorage<NewsMessage> newsStorage)
                newsStorage.ClearCache();
            else if (storage is SharedMarketDataStorage<BoardStateMessage> boardStorage)
                boardStorage.ClearCache();
        }
    }

    /// <summary>
    /// Gets the total number of cached storage instances.
    /// </summary>
    public int CachedStorageCount => _storageCache.Count;
}
