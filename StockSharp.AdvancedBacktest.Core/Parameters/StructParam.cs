namespace StockSharp.AdvancedBacktest.Parameters;

// Parameter type for value types (enums, structs) with discrete optimization ranges.
// Used for enum parameters like IndicatorType, PositionSizingMethod, StopLossMethod, etc.
public class StructParam<T> : CustomParam<T>
    where T : struct
{
    protected IList<T> Values { get; }

    public override IEnumerable<T> OptimizationRange => Values;

    public override IEnumerable<ICustomParam> OptimizationRangeParams =>
        Values.Select(value => new StructParam<T>(Id, [value]));

    public StructParam(string id, IList<T> values)
        : base(id, values.FirstOrDefault())
    {
        if (values.Count == 0)
            throw new System.ArgumentException("Values list cannot be empty", nameof(values));

        Values = values;
    }
}
