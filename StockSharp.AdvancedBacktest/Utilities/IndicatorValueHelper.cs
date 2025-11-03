using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Export;

namespace StockSharp.AdvancedBacktest.Utilities;

public static class IndicatorValueHelper
{
    public static IndicatorDataPoint ToDataPoint(IIndicatorValue value, TimeSpan? candleInterval)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var timestamp = GetAdjustedTimestamp(value, candleInterval);

        return new IndicatorDataPoint
        {
            Time = timestamp.ToUnixTimeMilliseconds(),
            Value = (double)value.GetValue<decimal>()
        };
    }

    public static DateTimeOffset GetAdjustedTimestamp(IIndicatorValue value, TimeSpan? candleInterval)
    {
        if (value == null)
            return DateTimeOffset.MinValue;

        if (TryGetShift(value, out int shift) && shift > 0 && candleInterval.HasValue)
        {
            var adjustment = TimeSpan.FromTicks(candleInterval.Value.Ticks * shift);
            return value.Time - adjustment;
        }

        return value.Time;
    }

    public static bool TryGetShift(IIndicatorValue value, out int shift)
    {
        shift = 0;

        if (value == null)
            return false;

        if (value is ShiftedIndicatorValue shiftedValue)
        {
            shift = shiftedValue.Shift;
            return true;
        }

        try
        {
            var shiftProperty = value.GetType().GetProperty("Shift");
            if (shiftProperty?.PropertyType == typeof(int))
            {
                var shiftObj = shiftProperty.GetValue(value);
                if (shiftObj != null)
                {
                    shift = (int)shiftObj;
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public static bool ShouldExport(IIndicatorValue value)
    {
        if (value == null)
            return false;

        if (!value.IsFormed)
            return false;

        if (value.IsEmpty)
            return false;

        try
        {
            var decimalValue = value.GetValue<decimal>();
            if (decimalValue == 0m)
                return false;
        }
        catch
        {
        }

        return true;
    }
}
