using Microsoft.Extensions.Logging;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

public class PerformanceTracker : IPerformanceTracker
{
    private readonly ILogger<PerformanceTracker> _logger;
    private readonly CircularBuffer<decimal> _returns;
    private readonly CircularBuffer<decimal> _portfolioValues;
    private readonly CircularBuffer<PerformanceSnapshot> _history;
    private readonly ConcurrentQueue<Trade> _trades;

    private decimal _currentValue;
    private decimal _initialValue = 100_000m; // Default starting value
    private decimal _maxValue;
    private decimal _maxDrawdownValue;
    private volatile int _totalTrades;
    private volatile int _winningTrades;
    private volatile bool _isDisposed;

    private readonly object _calculationLock = new();

    public decimal CurrentValue => _currentValue;

    public decimal TotalReturn => _initialValue > 0 ? (_currentValue - _initialValue) / _initialValue : 0m;

    public decimal SharpeRatio
    {
        get
        {
            lock (_calculationLock)
            {
                if (_returns.Count < 2)
                    return 0m;

                var volatility = CalculateVolatility();
                return volatility > 0 ? (TotalReturn * 252m) / volatility : 0m; // Annualized
            }
        }
    }

    public decimal MaxDrawdown => _maxValue > 0 ? Math.Max(0m, (_maxValue - _maxDrawdownValue) / _maxValue) : 0m;

    public decimal CurrentDrawdown => _maxValue > 0 ? Math.Max(0m, (_maxValue - _currentValue) / _maxValue) : 0m;

    public decimal WinRate => _totalTrades > 0 ? (decimal)_winningTrades / _totalTrades : 0m;

    public int TotalTrades => _totalTrades;

    public int WinningTrades => _winningTrades;

    public bool IsConsistent => _currentValue >= 0 && _maxValue >= _currentValue && _totalTrades >= 0;

    public PerformanceTracker(ILogger<PerformanceTracker> logger, int historySize = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _returns = new CircularBuffer<decimal>(historySize);
        _portfolioValues = new CircularBuffer<decimal>(historySize);
        _history = new CircularBuffer<PerformanceSnapshot>(historySize);
        _trades = new ConcurrentQueue<Trade>();

        _currentValue = _initialValue;
        _maxValue = _initialValue;
        _maxDrawdownValue = _initialValue;

        _logger.LogDebug("Performance tracker initialized with history size {HistorySize}", historySize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTrade(Trade trade)
    {
        if (_isDisposed || trade == null)
            return;

        try
        {
            _trades.Enqueue(trade);
            Interlocked.Increment(ref _totalTrades);

            // For now, we can't determine win/loss from trade alone
            // This would need to be calculated based on position tracking
            // Interlocked.Increment(ref _winningTrades);

            _logger.LogTrace("Recorded trade {TradeId} for {SecurityCode}", trade.Id, trade.Security?.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording trade {TradeId}", trade.Id);
        }
    }

    public void UpdatePortfolioValue(decimal value, DateTimeOffset timestamp)
    {
        if (_isDisposed || value < 0)
            return;

        try
        {
            lock (_calculationLock)
            {
                var previousValue = _currentValue;
                _currentValue = value;

                // Update maximum value
                if (value > _maxValue)
                {
                    _maxValue = value;
                }

                // Update maximum drawdown tracking
                if (value < _maxDrawdownValue)
                {
                    _maxDrawdownValue = value;
                }

                // Calculate return
                if (previousValue > 0)
                {
                    var dailyReturn = (value - previousValue) / previousValue;
                    _returns.Add(dailyReturn);
                }

                // Add to portfolio value history
                _portfolioValues.Add(value);

                // Create and store snapshot
                var snapshot = CreateSnapshot(timestamp);
                _history.Add(snapshot);
            }

            _logger.LogTrace("Updated portfolio value to {Value:C} at {Timestamp}", value, timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating portfolio value to {Value:C}", value);
        }
    }

    public decimal CalculateVolatility(int periods = 252)
    {
        if (_isDisposed)
            return 0m;

        lock (_calculationLock)
        {
            var count = Math.Min(periods, _returns.Count);
            if (count < 2)
                return 0m;

            // Use stack allocation for small arrays
            Span<decimal> returns = count <= 1000
                ? stackalloc decimal[count]
                : new decimal[count];

            _returns.CopyTo(returns, count);
            return CalculateStandardDeviation(returns) * (decimal)Math.Sqrt(252); // Annualized
        }
    }

    public PerformanceSnapshot GetSnapshot()
    {
        if (_isDisposed)
            return PerformanceSnapshot.Empty;

        return CreateSnapshot(DateTimeOffset.UtcNow);
    }

    public ImmutableArray<PerformanceSnapshot> GetHistory(DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        if (_isDisposed)
            return ImmutableArray<PerformanceSnapshot>.Empty;

        lock (_calculationLock)
        {
            var snapshots = _history.ToArray();

            if (from.HasValue || to.HasValue)
            {
                snapshots = snapshots.Where(s =>
                    (!from.HasValue || s.Timestamp >= from.Value) &&
                    (!to.HasValue || s.Timestamp <= to.Value)
                ).ToArray();
            }

            return snapshots.ToImmutableArray();
        }
    }

    public void Reset()
    {
        if (_isDisposed)
            return;

        lock (_calculationLock)
        {
            _currentValue = _initialValue;
            _maxValue = _initialValue;
            _maxDrawdownValue = _initialValue;
            _totalTrades = 0;
            _winningTrades = 0;

            _returns.Clear();
            _portfolioValues.Clear();
            _history.Clear();

            // Clear trades queue
            while (_trades.TryDequeue(out _)) { }
        }

        _logger.LogInformation("Performance tracker reset");
    }

    private PerformanceSnapshot CreateSnapshot(DateTimeOffset timestamp)
    {
        // Calculate daily PnL
        var dailyPnL = _portfolioValues.Count >= 2
            ? _currentValue - _portfolioValues[_portfolioValues.Count - 2]
            : 0m;

        return new PerformanceSnapshot(
            Timestamp: timestamp,
            PortfolioValue: _currentValue,
            TotalReturn: TotalReturn,
            SharpeRatio: SharpeRatio,
            MaxDrawdown: MaxDrawdown,
            CurrentDrawdown: CurrentDrawdown,
            WinRate: WinRate,
            TotalTrades: _totalTrades,
            WinningTrades: _winningTrades,
            Volatility: CalculateVolatility(),
            DailyPnL: dailyPnL
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static decimal CalculateStandardDeviation(ReadOnlySpan<decimal> values)
    {
        if (values.Length < 2)
            return 0m;

        // Calculate mean
        decimal sum = 0m;
        foreach (var value in values)
        {
            sum += value;
        }
        var mean = sum / values.Length;

        // Calculate variance
        decimal sumSquaredDiffs = 0m;
        foreach (var value in values)
        {
            var diff = value - mean;
            sumSquaredDiffs += diff * diff;
        }

        var variance = sumSquaredDiffs / (values.Length - 1);
        return (decimal)Math.Sqrt((double)variance);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _returns.Clear();
            _portfolioValues.Clear();
            _history.Clear();

            while (_trades.TryDequeue(out _)) { }

            _isDisposed = true;
            _logger.LogDebug("Performance tracker disposed");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

internal class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _tail;
    private int _count;
    private readonly int _capacity;

    public int Count => _count;
    public int Capacity => _capacity;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        _buffer[_tail] = item;
        _tail = (_tail + 1) % _capacity;

        if (_count < _capacity)
        {
            _count++;
        }
        else
        {
            _head = (_head + 1) % _capacity;
        }
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _buffer[(_head + index) % _capacity];
        }
    }

    public void CopyTo(Span<T> destination, int count = -1)
    {
        if (count == -1)
            count = _count;

        count = Math.Min(count, Math.Min(_count, destination.Length));

        for (int i = 0; i < count; i++)
        {
            destination[i] = this[_count - count + i];
        }
    }

    public T[] ToArray()
    {
        var result = new T[_count];
        for (int i = 0; i < _count; i++)
        {
            result[i] = this[i];
        }
        return result;
    }

    public void Clear()
    {
        _head = 0;
        _tail = 0;
        _count = 0;
        Array.Clear(_buffer, 0, _capacity);
    }
}