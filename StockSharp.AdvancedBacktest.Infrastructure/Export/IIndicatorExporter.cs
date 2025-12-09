using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Export;

public interface IIndicatorExporter
{
    IndicatorDataSeries ExtractSeries(IIndicator indicator, string? color = null);
    List<IndicatorDataSeries> ExtractComplexIndicator(IIndicator indicator);
    string GetDefaultColor(IIndicator indicator);
}
