using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Export;

[Obsolete("Use BacktestExporter instead. This class is provided for backward compatibility only.")]
public class IndicatorExporter : BacktestExporter
{
    public IndicatorExporter(ILogger<IndicatorExporter>? logger = null)
        : base(logger as ILogger<BacktestExporter>)
    {
    }
}
