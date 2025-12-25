namespace StockSharp.AdvancedBacktest.OrderManagement;

/// <summary>
/// Immutable key for signal deduplication based on price levels.
/// </summary>
public record SignalKey(decimal EntryPrice, decimal StopLoss, decimal TakeProfit);

/// <summary>
/// Tracks last signal to prevent duplicate order generation.
/// </summary>
public class SignalDeduplicator
{
    private SignalKey? _lastSignal;

    /// <summary>
    /// Checks if the signal is a duplicate of the last generated signal.
    /// If not a duplicate, updates the last signal to the new values.
    /// </summary>
    /// <param name="entry">Entry price</param>
    /// <param name="sl">Stop-loss price</param>
    /// <param name="tp">Take-profit price</param>
    /// <returns>True if this is a duplicate of the last signal; otherwise false</returns>
    public bool IsDuplicate(decimal entry, decimal sl, decimal tp)
    {
        var key = new SignalKey(entry, sl, tp);
        if (_lastSignal == key)
            return true;

        _lastSignal = key;
        return false;
    }

    /// <summary>
    /// Resets the deduplicator state, clearing the last signal.
    /// Should be called when a position closes.
    /// </summary>
    public void Reset() => _lastSignal = null;
}
