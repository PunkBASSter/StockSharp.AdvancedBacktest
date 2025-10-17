using System.ComponentModel.DataAnnotations;
using Ecng.Collections;
using Ecng.Serialization;
using StockSharp.Localization;

namespace StockSharp.Algo.Indicators;

[Display(
    ResourceType = typeof(LocalizedStrings),
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

    private decimal _delta = 0.001m;

    [Display(
        ResourceType = typeof(LocalizedStrings),
        Name = "Delta",
        Description = "Price change.",
        GroupName = LocalizedStrings.GeneralKey)]
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
        => CalcZigZag(input, input.ToDecimal());

    protected ZigZagIndicatorValue CalcZigZag(IIndicatorValue input, decimal price)
    {
        if (input.IsFinal)
            _buffer.PushBack(price);

        if (!IsFormed)
            return new ZigZagIndicatorValue(this, input.Time);

        var lastExtremum = _lastExtremum ?? price;
        var isUpTrend = _isUpTrend ?? price >= _buffer[^2];

        // Use dynamic threshold based on last swing size (like Python DeltaZigZag)
        var threshold = _lastSwingSize > 0 ? _lastSwingSize * Delta : Delta;
        var changeTrend = false;

        if (isUpTrend)
        {
            if (lastExtremum < price)
                lastExtremum = price;
            else
                changeTrend = price <= (lastExtremum - threshold);
        }
        else
        {
            if (lastExtremum > price)
                lastExtremum = price;
            else
                changeTrend = price >= (lastExtremum + threshold);
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
                    _lastExtremum = price;
                    _shift = 1;
                    // Track swing size for dynamic threshold calculation
                    _lastSwingSize = Math.Abs(lastExtremum - price);
                }
            }
        }
        else
        {
            if (input.IsFinal)
            {
                _lastExtremum = lastExtremum;
                _isUpTrend = isUpTrend;
                _shift++;
            }
        }

        return new ZigZagIndicatorValue(this, input.Time);
    }

    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);

        Delta = storage.GetValue<decimal>(nameof(Delta));
    }

    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);

        storage.SetValue(nameof(Delta), Delta);
    }

    public override string ToString() => base.ToString() + $" D={Delta}";
}
