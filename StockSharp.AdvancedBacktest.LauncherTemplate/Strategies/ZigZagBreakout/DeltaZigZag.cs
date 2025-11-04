using System.ComponentModel.DataAnnotations;
using Ecng.Collections;
using Ecng.Serialization;
using StockSharp.Messages;

namespace StockSharp.Algo.Indicators;

[Display(
    Name = "Delta ZigZag",
    Description = "Delta ZigZag")]
[IndicatorIn(typeof(CandleIndicatorValue))]
[IndicatorOut(typeof(ZigZagIndicatorValue))]
public class DeltaZigZag : BaseIndicator
{
    private readonly CircularBufferEx<decimal> _buffer = new(2);
    private decimal? _lastExtremum;
    private int _shift;
    private bool? _isUpTrend;
    private decimal _lastSwingSize;

    public DeltaZigZag()
    {
    }

    private decimal _delta = 0.5m;
    private decimal? _minimumThreshold;

    [Display(
        Name = "Delta",
        Description = "Price change percentage (e.g., 0.5 = 50% retracement).",
        GroupName = "General")]
    public decimal Delta
    {
        get => _delta;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_delta == value)
                return;

            _delta = value;
            Reset();
        }
    }

    /// <summary>
    /// Minimum absolute threshold used when no swing history exists.
    /// If not set, uses Delta as absolute value (not recommended for dynamic pricing).
    /// Should be set based on security's price step (e.g., PriceStep * 10).
    /// </summary>
    [Display(
        Name = "Minimum Threshold",
        Description = "Minimum absolute threshold for initial swings.",
        GroupName = "General")]
    public decimal? MinimumThreshold
    {
        get => _minimumThreshold;
        set
        {
            if (value.HasValue && value.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Minimum threshold must be positive");

            _minimumThreshold = value;
            Reset();
        }
    }

    public override int NumValuesToInitialize => _buffer.Capacity;

    protected override bool CalcIsFormed() => _buffer.Capacity == _buffer.Count;

    public override void Reset()
    {
        _buffer.Clear();
        _lastExtremum = default;
        _shift = default;
        _isUpTrend = default;
        _lastSwingSize = default;

        base.Reset();
    }

    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        // Extract High and Low prices from candle for proper ZigZag calculation
        var candle = input.GetValue<ICandleMessage>();
        var high = candle.HighPrice;
        var low = candle.LowPrice;
        return CalcZigZag(input, high, low);
    }

    protected ZigZagIndicatorValue CalcZigZag(IIndicatorValue input, decimal high, decimal low)
    {
        // Use close price (midpoint) for buffer initialization
        var currentPrice = (high + low) / 2m;

        if (input.IsFinal)
            _buffer.PushBack(currentPrice);

        if (!IsFormed)
            return new ZigZagIndicatorValue(this, input.Time);

        var lastExtremum = _lastExtremum ?? currentPrice;
        var isUpTrend = _isUpTrend ?? currentPrice >= _buffer[^2];

        // Use dynamic threshold based on last swing size (like Python DeltaZigZag)
        // Fallback to MinimumThreshold if set, otherwise use Delta as absolute value
        var threshold = _lastSwingSize > 0
            ? _lastSwingSize * Delta
            : (_minimumThreshold ?? Delta);
        var changeTrend = false;
        var extremumUpdated = false;

        if (isUpTrend)
        {
            // During uptrend, track the highest high for peak detection
            if (lastExtremum < high)
            {
                lastExtremum = high;
                extremumUpdated = true;
            }
            else
                changeTrend = low <= (lastExtremum - threshold);
        }
        else
        {
            // During downtrend, track the lowest low for bottom detection
            if (lastExtremum > low)
            {
                lastExtremum = low;
                extremumUpdated = true;
            }
            else
                changeTrend = high >= (lastExtremum + threshold);
        }

        if (changeTrend)
        {
            try
            {
                return new ZigZagIndicatorValue(this, lastExtremum, _shift, input.Time, isUpTrend);
            }
            finally
            {
                if (input.IsFinal)
                {
                    _isUpTrend = !isUpTrend;
                    // When switching trends, use appropriate price:
                    // - Switching to downtrend: start from low
                    // - Switching to uptrend: start from high
                    _lastExtremum = _isUpTrend.Value ? high : low;
                    _shift = 1;
                    // Track swing size for dynamic threshold calculation
                    _lastSwingSize = Math.Abs(lastExtremum - _lastExtremum.Value);
                }
            }
        }
        else
        {
            if (input.IsFinal)
            {
                _lastExtremum = lastExtremum;
                _isUpTrend = isUpTrend;

                // If extremum was updated to current bar, reset shift to 1
                // Otherwise, extremum is one more bar away, so increment shift
                if (extremumUpdated)
                    _shift = 1;
                else
                    _shift++;
            }
        }

        return new ZigZagIndicatorValue(this, input.Time);
    }

    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);

        Delta = storage.GetValue<decimal>(nameof(Delta));
        MinimumThreshold = storage.GetValue<decimal?>(nameof(MinimumThreshold));
    }

    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);

        storage.SetValue(nameof(Delta), Delta);
        storage.SetValue(nameof(MinimumThreshold), MinimumThreshold);
    }

    public override string ToString()
    {
        var str = base.ToString() + $" D={Delta}";
        if (MinimumThreshold.HasValue)
            str += $" MinT={MinimumThreshold.Value}";
        return str;
    }
}
