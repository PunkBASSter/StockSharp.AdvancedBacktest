using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Indicators;

/// <summary>
/// Indicator value for DeltaZigZag that supports pending (tentative) and confirmed points.
/// </summary>
/// <remarks>
/// Pending points represent the current extremum being tracked, which can be redrawn
/// as new highs/lows are found. Confirmed points represent finalized reversals.
/// </remarks>
public class DeltaZigZagIndicatorValue : ZigZagIndicatorValue
{
    /// <summary>
    /// Creates a pending (tentative) point that can be redrawn.
    /// </summary>
    /// <param name="indicator">The indicator instance.</param>
    /// <param name="value">The extremum price value.</param>
    /// <param name="extremumTime">The timestamp of the bar where the extremum occurred.</param>
    /// <param name="currentTime">The current bar timestamp (when this value was emitted).</param>
    /// <param name="isUp">True for peak, false for trough.</param>
    public DeltaZigZagIndicatorValue(IIndicator indicator, decimal value,
        DateTime extremumTime, DateTime currentTime, bool isUp)
        : base(indicator, value, 0, currentTime, isUp)
    {
        IsPending = true;
        ExtremumTime = extremumTime;
    }

    /// <summary>
    /// Creates a confirmed point (reversal has been detected).
    /// </summary>
    /// <param name="indicator">The indicator instance.</param>
    /// <param name="value">The extremum price value.</param>
    /// <param name="shift">Number of bars back to the extremum.</param>
    /// <param name="time">The current bar timestamp.</param>
    /// <param name="isUp">True for peak, false for trough.</param>
    public DeltaZigZagIndicatorValue(IIndicator indicator, decimal value,
        int shift, DateTime time, bool isUp)
        : base(indicator, value, shift, time, isUp)
    {
        IsPending = false;
        ExtremumTime = null;
    }

    /// <summary>
    /// Creates an empty value (no significant point).
    /// </summary>
    /// <param name="indicator">The indicator instance.</param>
    /// <param name="time">The current bar timestamp.</param>
    public DeltaZigZagIndicatorValue(IIndicator indicator, DateTime time)
        : base(indicator, time)
    {
        IsPending = null;
        ExtremumTime = null;
    }

    /// <summary>
    /// Indicates whether this is a pending (tentative) point that can be redrawn.
    /// Null for empty values, true for pending, false for confirmed.
    /// </summary>
    public bool? IsPending { get; }

    /// <summary>
    /// The timestamp of the bar where the extremum occurred.
    /// Only set for pending points; confirmed points use Shift for this calculation.
    /// </summary>
    public DateTime? ExtremumTime { get; }

    /// <inheritdoc />
    public override IEnumerable<object> ToValues()
    {
        if (IsEmpty)
            yield break;

        foreach (var v in base.ToValues())
            yield return v;

        yield return IsPending ?? false;
        if (ExtremumTime.HasValue)
            yield return ExtremumTime.Value;
    }

    /// <inheritdoc />
    public override void FromValues(object[] values)
    {
        if (values.Length == 0)
            return;

        base.FromValues(values);

        // Note: IsPending and ExtremumTime are readonly, so FromValues
        // cannot fully restore them. This is acceptable as serialization
        // is primarily for display purposes.
    }
}
