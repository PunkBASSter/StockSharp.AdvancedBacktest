using StockSharp.Algo.Indicators;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Export;

public class BacktestExporter : IIndicatorExporter
{
    private readonly IndicatorDataExtractor _extractor;
    private readonly ILogger<BacktestExporter>? _logger;

    public BacktestExporter(ILogger<BacktestExporter>? logger = null)
    {
        _logger = logger;
        _extractor = new IndicatorDataExtractor(logger as ILogger<IndicatorDataExtractor>);
    }

    public IndicatorDataSeries ExtractSeries(IIndicator indicator, string? color = null)
    {
        return ExtractSeries(indicator, null, color);
    }

    public IndicatorDataSeries ExtractSeries(IIndicator indicator, TimeSpan? candleInterval, string? color = null)
    {
        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        _logger?.LogDebug("Extracting series for indicator {Name} with interval {Interval}",
            indicator.Name, candleInterval);

        return _extractor.ExtractFromContainer(indicator, candleInterval, customColor: color);
    }

    public List<IndicatorDataSeries> ExtractComplexIndicator(IIndicator indicator)
    {
        return ExtractComplexIndicator(indicator, null);
    }

    public List<IndicatorDataSeries> ExtractComplexIndicator(IIndicator indicator, TimeSpan? candleInterval)
    {
        if (indicator == null)
            throw new ArgumentNullException(nameof(indicator));

        _logger?.LogDebug("Extracting complex indicator {Name} with interval {Interval}",
            indicator.Name, candleInterval);

        return _extractor.ExtractComplexIndicator(indicator, candleInterval);
    }

    public List<IndicatorDataSeries> ExtractIndicators(IEnumerable<IIndicator> indicators, TimeSpan? candleInterval)
    {
        if (indicators is null)
            throw new ArgumentNullException(nameof(indicators));

        _logger?.LogDebug("Batch extracting indicators with interval {Interval}", candleInterval);

        var allSeries = indicators
            .SelectMany(ind => _extractor.ExtractComplexIndicator(ind, candleInterval))
            .ToList();

        _logger?.LogDebug("Extracted {Count} total series from batch", allSeries.Count);
        return allSeries;
    }

    public string GetDefaultColor(IIndicator indicator)
    {
        return _extractor.GetDefaultColor(indicator);
    }
}
