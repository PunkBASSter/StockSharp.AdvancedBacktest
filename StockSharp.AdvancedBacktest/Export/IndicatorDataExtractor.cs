using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.Utilities;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Export;

/// <summary>
/// Single source of truth for extracting indicator data for export.
/// Handles both post-backtest extraction (from Container) and real-time extraction (from IIndicatorValue).
/// Uses IndicatorValueHelper for consistent shift correction and filtering.
/// </summary>
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

        // Iterate in reverse to get chronological order
        for (int i = containerCount - 1; i >= 0; i--)
        {
            try
            {
                var (input, output) = indicator.Container.GetValue(i);

                // Use IndicatorValueHelper for consistent filtering
                if (!IndicatorValueHelper.ShouldExport(output))
                {
                    _logger?.LogTrace("Skipping non-exportable value at index {Index}", i);
                    continue;
                }

                // Use IndicatorValueHelper for shift-aware timestamp and conversion
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

        // Use IndicatorValueHelper for filtering
        if (!IndicatorValueHelper.ShouldExport(value))
            return null;

        try
        {
            // Use IndicatorValueHelper for shift-aware extraction
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

        // Check if this is a complex indicator with inner indicators
        var innerIndicatorsProperty = indicator.GetType().GetProperty("InnerIndicators");
        if (innerIndicatorsProperty != null)
        {
            var innerIndicators = innerIndicatorsProperty.GetValue(indicator) as System.Collections.IEnumerable;
            if (innerIndicators != null)
            {
                // Check if there are actually any inner indicators
                var hasInnerIndicators = false;
                foreach (IIndicator innerIndicator in innerIndicators)
                {
                    hasInnerIndicators = true;
                    // Recursively extract each inner indicator
                    seriesList.Add(ExtractFromContainer(innerIndicator, candleInterval));
                }

                // Only return if we actually found inner indicators
                if (hasInnerIndicators)
                {
                    _logger?.LogDebug("Extracted complex indicator {Name} with {Count} inner indicators",
                        indicator.Name, seriesList.Count);
                    return seriesList;
                }
            }
        }

        // Not a complex indicator, extract as single series
        _logger?.LogDebug("Extracting simple indicator {Name}", indicator.Name);
        seriesList.Add(ExtractFromContainer(indicator, candleInterval));
        return seriesList;
    }

    public string GetDefaultColor(IIndicator indicator)
    {
        if (indicator == null)
            return "#607D8B"; // Default grey

        var name = indicator.Name?.ToLower() ?? indicator.GetType().Name.ToLower();

        var color = name switch
        {
            var n when n.Contains("sma") || n.Contains("simple") => "#2196F3",      // Blue
            var n when n.Contains("ema") || n.Contains("exponential") => "#FF9800", // Orange
            var n when n.Contains("jma") || n.Contains("jurik") => "#4ECDC4",       // Teal
            var n when n.Contains("rsi") => "#9C27B0",                              // Purple
            var n when n.Contains("macd") => "#4CAF50",                             // Green
            var n when n.Contains("bollinger") => "#F44336",                        // Red
            var n when n.Contains("zigzag") || n.Contains("dzz") || n.Contains("delta") => "#FF6B35", // Orange-red
            var n when n.Contains("atr") => "#795548",                              // Brown
            var n when n.Contains("stochastic") => "#E91E63",                       // Pink
            _ => "#607D8B"                                                          // Default grey
        };

        _logger?.LogTrace("Assigned color {Color} to indicator {Name}", color, indicator.Name);
        return color;
    }
}
