using System.ComponentModel.DataAnnotations;
using Ecng.Serialization;
using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Indicators;

/// <summary>
/// Derived indicator that filters DeltaZigZag to output only troughs.
/// </summary>
/// <remarks>
/// Useful for frontend visualization where you want separate peak and trough series
/// without double values on the same timestamp.
/// </remarks>
[Display(Name = "DeltaZzTrough", Description = "Filters DeltaZigZag to output only troughs")]
[IndicatorIn(typeof(CandleIndicatorValue))]
[IndicatorOut(typeof(DeltaZigZagIndicatorValue))]
public class DeltaZzTrough : BaseIndicator
{
    private readonly DeltaZigZag _deltaZigZag = new();

    public DeltaZzTrough()
    {
        AddResetTracking(_deltaZigZag);
    }

    /// <summary>
    /// Percentage of last swing size required for reversal confirmation.
    /// </summary>
    [Display(Name = "Delta", Description = "Percentage of last swing size required for reversal (0.0-1.0)", GroupName = "Parameters")]
    [Range(0.0, 1.0)]
    public decimal Delta
    {
        get => _deltaZigZag.Delta;
        set => _deltaZigZag.Delta = value;
    }

    /// <summary>
    /// Absolute minimum threshold used when no prior swing exists.
    /// </summary>
    [Display(Name = "MinimumThreshold", Description = "Absolute minimum threshold when no prior swing exists", GroupName = "Parameters")]
    public decimal MinimumThreshold
    {
        get => _deltaZigZag.MinimumThreshold;
        set => _deltaZigZag.MinimumThreshold = value;
    }

    /// <inheritdoc />
    public override int NumValuesToInitialize => _deltaZigZag.NumValuesToInitialize;

    /// <inheritdoc />
    protected override bool CalcIsFormed() => _deltaZigZag.IsFormed;

    /// <inheritdoc />
    protected override IIndicatorValue OnProcess(IIndicatorValue input)
    {
        var result = _deltaZigZag.Process(input);

        // Check if result is DeltaZigZagIndicatorValue (with IsPending info)
        if (result is DeltaZigZagIndicatorValue dzzResult)
        {
            // Only propagate troughs (IsUp = false)
            if (!dzzResult.IsEmpty && !dzzResult.IsUp)
            {
                // Propagate pending/confirmed state
                if (dzzResult.IsPending == true)
                {
                    // Pending trough - use extremum time for positioning
                    return new DeltaZigZagIndicatorValue(
                        this,
                        dzzResult.GetValue<decimal>(null),
                        dzzResult.ExtremumTime ?? input.Time,
                        input.Time,
                        isUp: false);
                }
                else
                {
                    // Confirmed trough
                    return new DeltaZigZagIndicatorValue(
                        this,
                        dzzResult.GetValue<decimal>(null),
                        dzzResult.Shift,
                        input.Time,
                        isUp: false);
                }
            }

            return new DeltaZigZagIndicatorValue(this, input.Time);
        }

        // Fallback for plain ZigZagIndicatorValue (shouldn't happen with current impl)
        var zigZagResult = (ZigZagIndicatorValue)result;
        if (!zigZagResult.IsEmpty && !zigZagResult.IsUp)
        {
            return new ZigZagIndicatorValue(this, zigZagResult.GetValue<decimal>(null), zigZagResult.Shift, input.Time, isUp: false);
        }

        return new ZigZagIndicatorValue(this, input.Time);
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
