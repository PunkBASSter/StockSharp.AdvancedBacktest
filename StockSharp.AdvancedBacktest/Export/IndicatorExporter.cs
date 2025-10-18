using StockSharp.Algo.Indicators;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Export;

/// <summary>
/// Default implementation of indicator data extraction service
/// </summary>
public class IndicatorExporter : IIndicatorExporter
{
    private readonly ILogger<IndicatorExporter>? _logger;

    public IndicatorExporter(ILogger<IndicatorExporter>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IndicatorDataSeries ExtractSeries(IIndicator indicator, string? color = null)
    {
        var series = new IndicatorDataSeries
        {
            Name = indicator.Name ?? indicator.GetType().Name,
            Color = color ?? GetDefaultColor(indicator)
        };

        // Extract values from indicator's Container (reverse to get chronological order)
        var values = new List<IndicatorDataPoint>();
        var containerCount = indicator.Container.Count;

        _logger?.LogDebug("Extracting {Count} values from indicator {Name}", containerCount, series.Name);

        for (int i = containerCount - 1; i >= 0; i--)
        {
            try
            {
                var (input, output) = indicator.Container.GetValue(i);

                if (output.IsEmpty)
                {
                    _logger?.LogTrace("Skipping empty value at index {Index}", i);
                    continue;
                }

                // Handle different indicator value types
                decimal value;
                try
                {
                    value = output.GetValue<decimal>();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to convert value at index {Index} to decimal, skipping", i);
                    continue;
                }

                values.Add(new IndicatorDataPoint
                {
                    Time = output.Time.ToUnixTimeSeconds(),
                    Value = (double)value
                });
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

    /// <inheritdoc/>
    public List<IndicatorDataSeries> ExtractComplexIndicator(IIndicator indicator)
    {
        var seriesList = new List<IndicatorDataSeries>();

        // Check if this is a complex indicator with inner indicators
        var innerIndicatorsProperty = indicator.GetType().GetProperty("InnerIndicators");
        if (innerIndicatorsProperty != null)
        {
            var innerIndicators = innerIndicatorsProperty.GetValue(indicator) as System.Collections.IEnumerable;
            if (innerIndicators != null)
            {
                _logger?.LogDebug("Extracting complex indicator {Name} with inner indicators", indicator.Name);

                foreach (IIndicator innerIndicator in innerIndicators)
                {
                    seriesList.Add(ExtractSeries(innerIndicator));
                }
                return seriesList;
            }
        }

        // Not a complex indicator, extract as single series
        _logger?.LogDebug("Extracting simple indicator {Name}", indicator.Name);
        seriesList.Add(ExtractSeries(indicator));
        return seriesList;
    }

    /// <inheritdoc/>
    public string GetDefaultColor(IIndicator indicator)
    {
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
