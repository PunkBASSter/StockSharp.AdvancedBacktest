using System.ComponentModel.DataAnnotations;
using Ecng.Serialization;
using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Indicators;

/// <summary>
/// DeltaZigZag indicator with dynamic volatility-based reversal thresholds.
/// </summary>
/// <remarks>
/// Unlike the standard ZigZag which uses a fixed percentage threshold, DeltaZigZag
/// calculates the reversal threshold dynamically based on the previous swing size.
/// </remarks>
[Display(Name = "DeltaZigZag", Description = "ZigZag indicator with dynamic volatility-based thresholds")]
[IndicatorIn(typeof(CandleIndicatorValue))]
[IndicatorOut(typeof(DeltaZigZagIndicatorValue))]
public class DeltaZigZag : BaseIndicator
{
    private bool? _isUpTrend;
    private decimal? _currentExtremum;
    private decimal? _lastPeakPrice;
    private decimal? _lastTroughPrice;
    private decimal? _lastSwingSize;
    private int _extremumBarIndex;
    private int _currentBarIndex;
    private DateTime _extremumTime;

    public DeltaZigZag()
    {
    }

    private decimal _delta = 0.5m;

    /// <summary>
    /// Percentage of last swing size required for reversal confirmation.
    /// </summary>
    /// <remarks>
    /// Value between 0.0 and 1.0. Default is 0.5 (50% retracement).
    /// </remarks>
    [Display(Name = "Delta", Description = "Percentage of last swing size required for reversal (0.0-1.0)", GroupName = "Parameters")]
    [Range(0.0, 1.0)]
    public decimal Delta
    {
        get => _delta;
        set
        {
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Delta must be between 0 and 1.");

            if (_delta == value)
                return;

            _delta = value;
            Reset();
        }
    }

    private decimal _minimumThreshold = 10m;

    /// <summary>
    /// Absolute minimum threshold used when no prior swing exists.
    /// </summary>
    [Display(Name = "MinimumThreshold", Description = "Absolute minimum threshold when no prior swing exists", GroupName = "Parameters")]
    public decimal MinimumThreshold
    {
        get => _minimumThreshold;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "MinimumThreshold must be greater than 0.");

            if (_minimumThreshold == value)
                return;

            _minimumThreshold = value;
            Reset();
        }
    }

    /// <inheritdoc />
    public override int NumValuesToInitialize => 1;

    /// <inheritdoc />
    protected override bool CalcIsFormed() => _isUpTrend.HasValue;

    /// <inheritdoc />
    public override void Reset()
    {
        _isUpTrend = null;
        _currentExtremum = null;
        _lastPeakPrice = null;
        _lastTroughPrice = null;
        _lastSwingSize = null;
        _extremumBarIndex = 0;
        _currentBarIndex = 0;
        _extremumTime = default;

        base.Reset();
    }

    /// <inheritdoc />
    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        var candle = input.ToCandle();
        var high = candle.HighPrice;
        var low = candle.LowPrice;
        var open = candle.OpenPrice;
        var close = candle.ClosePrice;

        if (!_isUpTrend.HasValue)
        {
            if (input.IsFinal)
            {
                _isUpTrend = DetermineInitialDirection(open, high, low, close);
                _currentExtremum = _isUpTrend.Value ? high : low;
                _extremumBarIndex = _currentBarIndex;
                _extremumTime = input.Time;
                _currentBarIndex++;

                // Emit initial pending point
                return new DeltaZigZagIndicatorValue(this, _currentExtremum.Value,
                    _extremumTime, input.Time, isUp: _isUpTrend.Value);
            }

            return new DeltaZigZagIndicatorValue(this, input.Time);
        }

        var isUpTrend = _isUpTrend.Value;
        var currentExtremum = _currentExtremum!.Value;
        var threshold = CalculateThreshold();

        DeltaZigZagIndicatorValue? result = null;

        if (isUpTrend)
        {
            if (high > currentExtremum)
            {
                // New high found - emit pending peak
                if (input.IsFinal)
                {
                    _currentExtremum = high;
                    _extremumBarIndex = _currentBarIndex;
                    _extremumTime = input.Time;
                }

                // Emit pending peak point (extremum moved to new high)
                result = new DeltaZigZagIndicatorValue(this, high,
                    input.IsFinal ? _extremumTime : input.Time, input.Time, isUp: true);
            }
            else if (low <= currentExtremum - threshold)
            {
                // Reversal confirmed - emit confirmed peak
                var shift = _currentBarIndex - _extremumBarIndex;
                result = new DeltaZigZagIndicatorValue(this, currentExtremum, shift, input.Time, isUp: true);

                if (input.IsFinal)
                {
                    _lastSwingSize = _lastTroughPrice.HasValue
                        ? currentExtremum - _lastTroughPrice.Value
                        : null;
                    _lastPeakPrice = currentExtremum;
                    _isUpTrend = false;
                    _currentExtremum = low;
                    _extremumBarIndex = _currentBarIndex;
                    _extremumTime = input.Time;
                }
            }
        }
        else
        {
            if (low < currentExtremum)
            {
                // New low found - emit pending trough
                if (input.IsFinal)
                {
                    _currentExtremum = low;
                    _extremumBarIndex = _currentBarIndex;
                    _extremumTime = input.Time;
                }

                // Emit pending trough point (extremum moved to new low)
                result = new DeltaZigZagIndicatorValue(this, low,
                    input.IsFinal ? _extremumTime : input.Time, input.Time, isUp: false);
            }
            else if (high >= currentExtremum + threshold)
            {
                // Reversal confirmed - emit confirmed trough
                var shift = _currentBarIndex - _extremumBarIndex;
                result = new DeltaZigZagIndicatorValue(this, currentExtremum, shift, input.Time, isUp: false);

                if (input.IsFinal)
                {
                    _lastSwingSize = _lastPeakPrice.HasValue
                        ? _lastPeakPrice.Value - currentExtremum
                        : null;
                    _lastTroughPrice = currentExtremum;
                    _isUpTrend = true;
                    _currentExtremum = high;
                    _extremumBarIndex = _currentBarIndex;
                    _extremumTime = input.Time;
                }
            }
        }

        if (input.IsFinal)
            _currentBarIndex++;

        return result ?? new DeltaZigZagIndicatorValue(this, input.Time);
    }

    private bool DetermineInitialDirection(decimal open, decimal high, decimal low, decimal close)
    {
        if (close > open)
            return true;

        if (close < open)
            return false;

        var upperWick = high - open;
        var lowerWick = open - low;

        return upperWick >= lowerWick;
    }

    private decimal CalculateThreshold()
    {
        if (!_lastSwingSize.HasValue || _lastSwingSize.Value <= 0)
            return _minimumThreshold;

        var dynamicThreshold = _delta * _lastSwingSize.Value;
        return Math.Max(dynamicThreshold, _minimumThreshold);
    }

    /// <inheritdoc />
    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);

        Delta = storage.GetValue<decimal>(nameof(Delta));
        MinimumThreshold = storage.GetValue<decimal>(nameof(MinimumThreshold));
    }

    /// <inheritdoc />
    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);

        storage.SetValue(nameof(Delta), Delta);
        storage.SetValue(nameof(MinimumThreshold), MinimumThreshold);
    }

    /// <inheritdoc />
    public override string ToString() => base.ToString() + $" Delta={Delta} MinThreshold={MinimumThreshold}";
}
