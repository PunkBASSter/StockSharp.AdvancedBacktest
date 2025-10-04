using System.Collections.Generic;
using System.Numerics;
using StockSharp.Algo.Strategies;

namespace StockSharp.AdvancedBacktest.Parameters;

public class NumberParam<T> : CustomParam<T>
    where T : struct, IAdditionOperators<T, T, T>, IComparisonOperators<T, T, bool>
{
    public NumberParam(string id, T defaultValue, T optimizeFrom = default, T optimizeTo = default, T optimizeStep = default)
        : base(id, defaultValue, optimizeFrom, optimizeTo, optimizeStep)
    {
    }

    public override IEnumerable<T> OptimizationRange
    {
        get
        {
            // If no optimization parameters are set, return only the default value
            if (OptimizeStep == null || OptimizeFrom == null || OptimizeTo == null ||
                EqualityComparer<T>.Default.Equals((T)OptimizeStep, default))
            {
                yield return Value;
                yield break;
            }

            for (var value = (T)OptimizeFrom; value <= (T)OptimizeTo; value += (T)OptimizeStep)
            {
                yield return value;
            }
        }
    }

    public override IEnumerable<ICustomParam> OptimizationRangeParams
    {
        get
        {
            // If no optimization parameters are set, return only this instance
            if (OptimizeStep == null || OptimizeFrom == null || OptimizeTo == null ||
                EqualityComparer<T>.Default.Equals((T)OptimizeStep, default))
            {
                yield return this;
                yield break;
            }

            for (var value = (T)OptimizeFrom; value <= (T)OptimizeTo; value += (T)OptimizeStep)
            {
                yield return new NumberParam<T>(Id, value);
            }
        }
    }
}
