using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Export;

namespace StockSharp.AdvancedBacktest.Utilities;

public static class IndicatorValueHelper
{
    public static IndicatorDataPoint ToDataPoint(IIndicatorValue value, TimeSpan? candleInterval)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        // Guard against empty values that would throw 'No data' exception
        if (value.IsEmpty)
            throw new InvalidOperationException("Cannot convert empty indicator value to data point. Use ShouldExport() to filter first.");

        var timestamp = GetAdjustedTimestamp(value, candleInterval);

        // Use safe extraction to avoid 'No data' exception from StockSharp
        decimal decimalValue;
        try
        {
            decimalValue = value.GetValue<decimal>();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No data"))
        {
            throw new InvalidOperationException($"Indicator value reported IsEmpty=false but contains no data. Time: {value.Time}, Type: {value.GetType().Name}", ex);
        }

        var dataPoint = new IndicatorDataPoint
        {
            Time = timestamp.ToUnixTimeMilliseconds(),
            Value = (double)decimalValue
        };

        // Extract ZigZag-specific properties if present
        ExtractZigZagProperties(value, dataPoint);

        return dataPoint;
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

        // Verify we can actually extract the value without exception
        try
        {
            var decimalValue = value.GetValue<decimal>();
            // Skip zero values (indicator outputs zero when no significant value)
            if (decimalValue == 0m)
                return false;
        }
        catch (InvalidOperationException)
        {
            // 'No data' exception - value is not exportable
            return false;
        }
        catch
        {
            // Any other extraction error - skip this value
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts ZigZag-specific properties (IsUp, IsPending, ExtremumTime) from indicator value.
    /// </summary>
    /// <param name="value">The indicator value to extract from.</param>
    /// <param name="dataPoint">The data point to populate with ZigZag properties.</param>
    public static void ExtractZigZagProperties(IIndicatorValue value, IndicatorDataPoint dataPoint)
    {
        if (value == null || dataPoint == null)
            return;

        var valueType = value.GetType();

        // Try to get IsUp property (for ZigZagIndicatorValue and derived types)
        try
        {
            var isUpProperty = valueType.GetProperty("IsUp");
            if (isUpProperty?.PropertyType == typeof(bool))
            {
                dataPoint.IsUp = (bool?)isUpProperty.GetValue(value);
            }
        }
        catch
        {
            // Ignore reflection errors
        }

        // Try to get IsPending property (for DeltaZigZagIndicatorValue)
        try
        {
            var isPendingProperty = valueType.GetProperty("IsPending");
            if (isPendingProperty?.PropertyType == typeof(bool?) || isPendingProperty?.PropertyType == typeof(bool))
            {
                var isPendingValue = isPendingProperty.GetValue(value);
                dataPoint.IsPending = isPendingValue as bool?;
            }
        }
        catch
        {
            // Ignore reflection errors
        }

        // Try to get ExtremumTime property (for DeltaZigZagIndicatorValue)
        try
        {
            var extremumTimeProperty = valueType.GetProperty("ExtremumTime");
            if (extremumTimeProperty?.PropertyType == typeof(DateTime?))
            {
                var extremumTime = (DateTime?)extremumTimeProperty.GetValue(value);
                if (extremumTime.HasValue)
                {
                    dataPoint.ExtremumTime = new DateTimeOffset(extremumTime.Value, TimeSpan.Zero)
                        .ToUnixTimeMilliseconds();
                }
            }
        }
        catch
        {
            // Ignore reflection errors
        }
    }
}
