# Report Export Integration

This directory contains classes for exporting backtest results to various formats, including interactive HTML reports using Next.js.

## ReportBuilder

The `ReportBuilder<TStrategy>` class provides two methods for generating reports:

### 1. GenerateInteractiveChart (Legacy)

Generates a single HTML file with embedded chart data.

```csharp
var reportBuilder = new ReportBuilder<MyStrategy>();
reportBuilder.GenerateInteractiveChart(model, openInBrowser: true);
```

### 2. GenerateReportAsync (Recommended)

Generates a static HTML report by copying a pre-built Next.js template and writing separate JSON data files.

```csharp
var reportBuilder = new ReportBuilder<MyStrategy>(logger);
await reportBuilder.GenerateReportAsync(model, outputPath);
```

## Integration with Next.js Web Template

### Prerequisites

1. Navigate to `StockSharp.AdvancedBacktest.Web`
2. Install dependencies: `npm install`
3. Build the static template: `npm run build`

This creates a static HTML bundle in `StockSharp.AdvancedBacktest.Web/out/`.

### How It Works

1. **Export Chart Data**: The method extracts candle data, trade data, and walk-forward analysis results from the backtest and serializes them to `chartData.json`

2. **Copy Template**: The pre-built Next.js template is copied from `StockSharp.AdvancedBacktest.Web/out/` to the output directory

3. **Verification**: The method verifies that `index.html` exists in the output directory

4. **Error Handling**: Comprehensive error handling ensures that:
   - Missing templates are detected with clear error messages
   - File copy failures are logged and reported
   - Existing `chartData.json` in the template is not overwritten

### Directory Structure

After running `GenerateReportAsync`, the output directory will contain:

```
Results/
└── {timestamp}/
    ├── index.html          # Main HTML page
    ├── chartData.json      # Backtest data (generated)
    ├── _next/              # Next.js static assets
    │   └── static/
    │       └── chunks/
    └── ...                 # Other Next.js files
```

### Usage Example

```csharp
using StockSharp.AdvancedBacktest.Export;
using Microsoft.Extensions.Logging;

// Create logger (optional but recommended)
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ReportBuilder<MyStrategy>>();

// Create report builder
var reportBuilder = new ReportBuilder<MyStrategy>(logger);

// Create model with backtest data
var model = new StrategySecurityChartModel
{
    StartDate = startDate,
    EndDate = endDate,
    HistoryPath = historyPath,
    Security = security,
    Strategy = strategy,
    OutputPath = outputPath,
    Metrics = metrics,
    WalkForwardResult = wfResult
};

// Generate report
var reportPath = Path.Combine("Results", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
await reportBuilder.GenerateReportAsync(model, reportPath);

Console.WriteLine($"Report generated at: {reportPath}");
```

### Custom Web Template Path

By default, the builder looks for the template at:
```
{AppDomain.CurrentDomain.BaseDirectory}/../../../../StockSharp.AdvancedBacktest.Web/out
```

You can specify a custom path:

```csharp
var customPath = @"C:\CustomTemplates\MyReport";
var reportBuilder = new ReportBuilder<MyStrategy>(logger, customPath);
```

## Chart Data Model

The exported `chartData.json` follows this structure:

```typescript
interface ChartDataModel {
  candles: CandleDataPoint[];
  indicators?: IndicatorDataSeries[];
  trades: TradeDataPoint[];
  walkForward?: WalkForwardDataModel;
}
```

See `StockSharp.AdvancedBacktest.Web/types/chart-data.ts` for complete TypeScript definitions.

## Error Handling

The method throws `InvalidOperationException` in these cases:

1. **Template not found**: When `StockSharp.AdvancedBacktest.Web/out` doesn't exist
   - Solution: Run `npm run build` in the web project

2. **Missing index.html**: When the template was copied but doesn't contain `index.html`
   - Solution: Verify the Next.js build completed successfully

3. **Directory access errors**: When file permissions prevent copying
   - Solution: Check directory permissions

## Logging

The class uses `ILogger<ReportBuilder<TStrategy>>` for structured logging:

- **Information**: Report generation start/completion
- **Debug**: Directory creation, file operations
- **Trace**: Individual file copies (verbose)
- **Error**: Exceptions with full details

## Testing

Integration tests are available in `StockSharp.AdvancedBacktest.Tests/ReportBuilderIntegrationTests.cs`:

```bash
dotnet test StockSharp.AdvancedBacktest.Tests/ --filter ReportBuilderIntegrationTests
```

## Performance Considerations

- **Template Pre-building**: The Next.js template should be built **once** before running optimizations, not for each report
- **Async Operation**: The method is async to avoid blocking during file I/O
- **File Copying**: Large templates may take time to copy; consider template size optimization
- **Parallel Generation**: Multiple reports can be generated concurrently by using different output paths

## Migration from Legacy Method

If you're currently using `GenerateInteractiveChart`:

```csharp
// Old (synchronous, single file)
reportBuilder.GenerateInteractiveChart(model, openInBrowser: true);

// New (async, static bundle)
await reportBuilder.GenerateReportAsync(model, outputPath);
```

Both methods are available for backwards compatibility.
