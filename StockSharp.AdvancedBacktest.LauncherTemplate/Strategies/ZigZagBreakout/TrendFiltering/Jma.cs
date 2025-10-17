using System.ComponentModel.DataAnnotations;
using Ecng.Collections;
using Ecng.Serialization;
using StockSharp.Algo.Indicators;
using StockSharp.Localization;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout.TrendFiltering;

[Display(
    ResourceType = typeof(LocalizedStrings),
    Name = "JMA",
    Description = "Jurik Moving Average")]
[IndicatorIn(typeof(CandleIndicatorValue))]
public class Jma : BaseIndicator
{
    private readonly CircularBufferEx<decimal> _buffer;
    private readonly List<decimal> _volty = new();
    private readonly List<decimal> _vSum = new();

    private decimal _kv;
    private decimal _det0;
    private decimal _det1;
    private decimal _ma1;
    private decimal _ma2;
    private decimal _uBand;
    private decimal _lBand;
    private decimal _jmaValue;

    private decimal _pr;
    private decimal _length1;
    private decimal _pow1;
    private decimal _bet;
    private decimal _beta;
    private decimal _adjustedLength;

    private const int SumLength = 10;

    public Jma()
    {
        _buffer = new CircularBufferEx<decimal>(7);
        Length = 7;
        Phase = 0;
    }

    private int _length = 7;

    [Display(
        ResourceType = typeof(LocalizedStrings),
        Name = "Length",
        Description = "Period",
        GroupName = LocalizedStrings.GeneralKey)]
    public int Length
    {
        get => _length;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_length == value)
                return;

            _length = value;
            _buffer.Capacity = value;
            RecalculateStaticParameters();
            Reset();
        }
    }

    private int _phase;

    [Display(
        ResourceType = typeof(LocalizedStrings),
        Name = "Phase",
        Description = "Phase",
        GroupName = LocalizedStrings.GeneralKey)]
    public int Phase
    {
        get => _phase;
        set
        {
            if (_phase == value)
                return;

            _phase = value;
            RecalculateStaticParameters();
            Reset();
        }
    }

    public override int NumValuesToInitialize => Length;

    protected override bool CalcIsFormed() => _buffer.Count >= Length;

    private void RecalculateStaticParameters()
    {
        _adjustedLength = 0.5m * (_length - 1);
        _pr = _phase < -100 ? 0.5m : _phase > 100 ? 2.5m : 1.5m + _phase * 0.01m;
        _length1 = Math.Max((decimal)(Math.Log(Math.Sqrt((double)_adjustedLength)) / Math.Log(2.0)) + 2.0m, 0m);
        _pow1 = Math.Max(_length1 - 2.0m, 0.5m);
        var length2 = _length1 * (decimal)Math.Sqrt((double)_adjustedLength);
        _bet = length2 / (length2 + 1);
        _beta = 0.45m * (_length - 1) / (0.45m * (_length - 1) + 2.0m);
    }

    public override void Reset()
    {
        _buffer.Clear();
        _volty.Clear();
        _vSum.Clear();

        _kv = 0;
        _det0 = 0;
        _det1 = 0;
        _ma1 = 0;
        _ma2 = 0;
        _uBand = 0;
        _lBand = 0;
        _jmaValue = 0;

        base.Reset();
    }

    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        var price = input.ToDecimal();

        if (input.IsFinal)
            _buffer.PushBack(price);

        // Initialize on first value
        if (_buffer.Count == 1)
        {
            _jmaValue = _ma1 = _uBand = _lBand = price;
            _volty.Add(0);
            _vSum.Add(0);
            return new DecimalIndicatorValue(this, _jmaValue, input.Time);
        }

        if (!IsFormed)
        {
            if (input.IsFinal)
            {
                _volty.Add(0);
                _vSum.Add(0);
            }
            return new DecimalIndicatorValue(this, price, input.Time);
        }

        // Price volatility
        var del1 = price - _uBand;
        var del2 = price - _lBand;
        var currentVolty = Math.Abs(del1) != Math.Abs(del2) ? Math.Max(Math.Abs(del1), Math.Abs(del2)) : 0;

        // Relative price volatility factor
        var i = input.IsFinal ? _volty.Count : _volty.Count - 1;
        var prevVSum = i > 0 ? _vSum[i - 1] : 0;
        var oldVolty = i >= SumLength ? _volty[i - SumLength] : 0;
        var currentVSum = prevVSum + (currentVolty - oldVolty) / SumLength;

        var startIdx = Math.Max(i - 65, 0);
        var avgVolty = _vSum.Skip(startIdx).Take(Math.Min(i - startIdx + 1, _vSum.Count - startIdx)).Average();
        var dVolty = avgVolty == 0 ? 0 : currentVolty / avgVolty;
        var rVolty = Math.Max(1.0m, Math.Min((decimal)Math.Pow((double)_length1, 1.0 / (double)_pow1), dVolty));

        // Jurik volatility bands
        var pow2 = (decimal)Math.Pow((double)rVolty, (double)_pow1);
        _kv = (decimal)Math.Pow((double)_bet, Math.Sqrt((double)pow2));
        _uBand = del1 > 0 ? price : price - (_kv * del1);
        _lBand = del2 < 0 ? price : price - (_kv * del2);

        // Jurik Dynamic Factor
        var power = (decimal)Math.Pow((double)rVolty, (double)_pow1);
        var alpha = (decimal)Math.Pow((double)_beta, (double)power);

        // 1st stage - preliminary smoothing by adaptive EMA
        _ma1 = ((1 - alpha) * price) + (alpha * _ma1);

        // 2nd stage - one more preliminary smoothing by Kalman filter
        _det0 = ((price - _ma1) * (1 - _beta)) + (_beta * _det0);
        _ma2 = _ma1 + _pr * _det0;

        // 3rd stage - final smoothing by unique Jurik adaptive filter
        _det1 = ((_ma2 - _jmaValue) * (1 - alpha) * (1 - alpha)) + (alpha * alpha * _det1);
        _jmaValue = _jmaValue + _det1;

        if (input.IsFinal)
        {
            _volty.Add(currentVolty);
            _vSum.Add(currentVSum);
        }

        return new DecimalIndicatorValue(this, _jmaValue, input.Time);
    }

    public override void Load(SettingsStorage storage)
    {
        base.Load(storage);

        Length = storage.GetValue<int>(nameof(Length));
        Phase = storage.GetValue<int>(nameof(Phase));
    }

    public override void Save(SettingsStorage storage)
    {
        base.Save(storage);

        storage.SetValue(nameof(Length), Length);
        storage.SetValue(nameof(Phase), Phase);
    }

    public override string ToString() => $"{base.ToString()} L={Length} P={Phase}";
}
