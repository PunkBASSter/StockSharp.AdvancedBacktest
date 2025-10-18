using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Export;

/// <summary>
/// Service for extracting indicator data for visualization
/// </summary>
public interface IIndicatorExporter
{
    /// <summary>
    /// Extracts indicator time series data from StockSharp indicator Container
    /// </summary>
    /// <param name="indicator">The indicator to extract data from</param>
    /// <param name="color">Color for the indicator series (optional, will use default if null)</param>
    /// <returns>IndicatorDataSeries with all historical values from the Container</returns>
    IndicatorDataSeries ExtractSeries(IIndicator indicator, string? color = null);

    /// <summary>
    /// Extracts all sub-indicators from a complex indicator (e.g., Bollinger Bands)
    /// </summary>
    /// <param name="indicator">The indicator to extract data from (may be simple or complex)</param>
    /// <returns>List of IndicatorDataSeries (one for simple indicators, multiple for complex)</returns>
    List<IndicatorDataSeries> ExtractComplexIndicator(IIndicator indicator);

    /// <summary>
    /// Get default color based on indicator type
    /// </summary>
    /// <param name="indicator">The indicator to get color for</param>
    /// <returns>Hex color string (e.g., "#2196F3")</returns>
    string GetDefaultColor(IIndicator indicator);
}
