using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Utilities;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Export;

public class IndicatorDataExtractor
{
    private readonly ILogger<IndicatorDataExtractor>? _logger;

    public IndicatorDataExtractor(ILogger<IndicatorDataExtractor>? logger = null)
    {
        _logger = logger;
    }

    public IndicatorDataSeries ExtractFromContainer(
        IIndicator indicator,
        TimeSpan? candleInterval,
        string? customName = null,
        string? customColor = null)
    {
        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        var series = new IndicatorDataSeries
        {
            Name = customName ?? indicator.Name ?? indicator.GetType().Name,
            Color = customColor ?? GetDefaultColor(indicator)
        };

        var values = new List<IndicatorDataPoint>();
        var containerCount = indicator.Container.Count;

        _logger?.LogDebug("Extracting {Count} values from indicator {Name} (interval: {Interval})",
            containerCount, series.Name, candleInterval);

        for (int i = containerCount - 1; i >= 0; i--)
        {
            try
            {
                var (input, output) = indicator.Container.GetValue(i);

                if (!IndicatorValueHelper.ShouldExport(output))
                {
                    _logger?.LogTrace("Skipping non-exportable value at index {Index}", i);
                    continue;
                }

                var dataPoint = IndicatorValueHelper.ToDataPoint(output, candleInterval);
                values.Add(dataPoint);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error extracting value at index {Index}, skipping", i);
            }
        }

        series.Values = values;
        _logger?.LogDebug("Extracted {Count} valid values for indicator {Name}", values.Count, series.Name);

        return series;
    }

    public IndicatorDataPoint? ExtractFromValue(
        IIndicatorValue value,
        TimeSpan? candleInterval)
    {
        if (value == null)
            return null;

        if (!IndicatorValueHelper.ShouldExport(value))
            return null;

        try
        {
            var dataPoint = IndicatorValueHelper.ToDataPoint(value, candleInterval);
            return dataPoint;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error extracting indicator value");
            return null;
        }
    }

    public List<IndicatorDataSeries> ExtractComplexIndicator(
        IIndicator indicator,
        TimeSpan? candleInterval)
    {
        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        var seriesList = new List<IndicatorDataSeries>();

        var innerIndicatorsProperty = indicator.GetType().GetProperty("InnerIndicators");
        if (innerIndicatorsProperty != null)
        {
            var innerIndicators = innerIndicatorsProperty.GetValue(indicator) as System.Collections.IEnumerable;
            if (innerIndicators != null)
            {
                var hasInnerIndicators = false;
                foreach (IIndicator innerIndicator in innerIndicators)
                {
                    hasInnerIndicators = true;
                    seriesList.Add(ExtractFromContainer(innerIndicator, candleInterval));
                }

                if (hasInnerIndicators)
                {
                    _logger?.LogDebug("Extracted complex indicator {Name} with {Count} inner indicators",
                        indicator.Name, seriesList.Count);
                    return seriesList;
                }
            }
        }

        _logger?.LogDebug("Extracting simple indicator {Name}", indicator.Name);
        seriesList.Add(ExtractFromContainer(indicator, candleInterval));
        return seriesList;
    }

    public string GetDefaultColor(IIndicator indicator)
    {
        if (indicator == null)
            return "#607D8B";

        var name = indicator.Name?.ToLower() ?? indicator.GetType().Name.ToLower();

        var color = name switch
        {
            var n when n.Contains("sma") || n.Contains("simple") => "#2196F3",
            var n when n.Contains("ema") || n.Contains("exponential") => "#FF9800",
            var n when n.Contains("jma") || n.Contains("jurik") => "#4ECDC4",
            var n when n.Contains("rsi") => "#9C27B0",
            var n when n.Contains("macd") => "#4CAF50",
            var n when n.Contains("bollinger") => "#F44336",
            var n when n.Contains("zigzag") || n.Contains("dzz") || n.Contains("delta") => "#FF6B35",
            var n when n.Contains("atr") => "#795548",
            var n when n.Contains("stochastic") => "#E91E63",
            _ => "#607D8B"
        };

        _logger?.LogTrace("Assigned color {Color} to indicator {Name}", color, indicator.Name);
        return color;
    }
}
