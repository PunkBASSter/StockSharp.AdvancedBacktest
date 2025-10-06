# StockSharp.AdvancedBacktest

A .NET 10 library extending StockSharp for advanced backtesting, optimization, and strategy validation. Features include multi-parameter optimization, walk-forward analysis, performance metrics calculation, and interactive HTML report generation.

## Features

- **Multi-Parameter Optimization**: Optimize combinations of symbols, timeframes, and strategy parameters
- **Walk-Forward Validation**: Robust out-of-sample testing with anchored or rolling windows
- **Performance Metrics**: Comprehensive statistics including Sharpe ratio, Sortino ratio, drawdown, profit factor
- **Interactive Reporting**: Generate HTML reports with candlestick charts, trade markers, and walk-forward analysis
- **Flexible Parameter Types**: Support for numeric ranges, securities, timeframes, and custom classes
- **Parallel Processing**: Multi-threaded optimization for faster results
- **Metric Filtering**: Select top strategies based on custom performance criteria

## Quick Start

### 1. Setup

Clone the repository with submodules:

```bash
git clone --recurse-submodules https://github.com/PunkBASSter/StockSharp.AdvancedBacktest.git
cd StockSharp.AdvancedBacktest
```

If already cloned, initialize submodules:

```bash
git submodule update --init --recursive
```

Build the solution:

```bash
dotnet build StockSharp.AdvancedBacktest.slnx
```

### 2. Install Package Reference

Add to your project:

```xml
<ProjectReference Include="path\to\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.csproj" />
```

## Usage Guide

### Creating a Strategy

Your strategy must inherit from `CustomStrategyBase`:

```csharp
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Parameters;

public class MyStrategy : CustomStrategyBase
{
    // Define parameters to optimize
    public int FastPeriod { get; set; } = 10;
    public int SlowPeriod { get; set; } = 30;

    public MyStrategy() : base()
    {
    }

    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);
        // Initialize indicators, subscriptions, etc.
    }
}
```

### Basic Optimization

```csharp
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.BusinessEntities;

// 1. Define parameter ranges to optimize
var paramsContainer = new CustomParamsContainer
{
    CustomParams = new()
    {
        // Optimize securities
        new SecurityParam("Security", canOptimize: true)
        {
            OptimizationRange = new Dictionary<Security, List<TimeSpan>>
            {
                {
                    new Security { Id = "BTCUSDT@BNB" },
                    new List<TimeSpan> { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) }
                },
                {
                    new Security { Id = "ETHUSDT@BNB" },
                    new List<TimeSpan> { TimeSpan.FromMinutes(5) }
                }
            }
        },

        // Optimize numeric parameters
        new NumberParam("FastPeriod", canOptimize: true, start: 5, end: 20, step: 5),
        new NumberParam("SlowPeriod", canOptimize: true, start: 20, end: 50, step: 10)
    },

    // Add validation rules (optional)
    ValidationRules = new()
    {
        // Ensure FastPeriod < SlowPeriod
        (dict) =>
        {
            var fast = ((NumberParam)dict["FastPeriod"]).Value;
            var slow = ((NumberParam)dict["SlowPeriod"]).Value;
            return fast < slow;
        }
    }
};

// 2. Configure optimization periods
var periodConfig = new OptimizationPeriodConfig
{
    TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
    TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
    ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
    ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)
};

// 3. Create optimization configuration
var config = new OptimizationConfig
{
    ParamsContainer = paramsContainer,
    TrainingPeriod = periodConfig,
    HistoryPath = @"C:\Data\History",  // Path to StockSharp historical data
    InitialCapital = 10000m,
    TradeVolume = 0.01m,
    ParallelWorkers = Environment.ProcessorCount,

    // Add metric filters to select top performers
    MetricFilters = new()
    {
        m => m.TotalReturn > 10,      // Min 10% return
        m => m.SharpeRatio > 1.5,     // Min Sharpe ratio
        m => m.MaxDrawdown > -15,     // Max 15% drawdown
        m => m.WinRate > 50           // Min 50% win rate
    }
};

// 4. Run optimization
var optimizer = new OptimizerRunner<MyStrategy>();
optimizer.CreateOptimizer(config);
var results = optimizer.Optimize();

// 5. Display top results
var topResults = results
    .OrderByDescending(r => r.Value.TrainingMetrics.SharpeRatio)
    .Take(10);

foreach (var result in topResults)
{
    Console.WriteLine($"Strategy: {result.Key}");
    Console.WriteLine(result.Value.TrainingMetrics.ToDetailedString());
    Console.WriteLine(result.Value.ValidationMetrics?.ToDetailedString() ?? "No validation");
    Console.WriteLine();
}
```

### Selecting Top N Strategies

```csharp
// Select top 5 by Sharpe ratio
var topBySharpe = results
    .Where(r => r.Value.TrainingMetrics != null)
    .OrderByDescending(r => r.Value.TrainingMetrics.SharpeRatio)
    .Take(5)
    .ToList();

// Select top 5 by total return
var topByReturn = results
    .Where(r => r.Value.TrainingMetrics != null)
    .OrderByDescending(r => r.Value.TrainingMetrics.TotalReturn)
    .Take(5)
    .ToList();

// Select top 5 by profit factor with minimum trade count
var topByProfitFactor = results
    .Where(r => r.Value.TrainingMetrics != null && r.Value.TrainingMetrics.TotalTrades >= 100)
    .OrderByDescending(r => r.Value.TrainingMetrics.ProfitFactor)
    .Take(5)
    .ToList();

// Select top 5 by validation performance (out-of-sample)
var topByValidation = results
    .Where(r => r.Value.ValidationMetrics != null)
    .OrderByDescending(r => r.Value.ValidationMetrics.SharpeRatio)
    .Take(5)
    .ToList();
```

### Walk-Forward Validation

Walk-forward analysis provides robust out-of-sample testing by optimizing on training windows and testing on following periods:

```csharp
using StockSharp.AdvancedBacktest.PerformanceValidation;

// 1. Create walk-forward configuration
var wfConfig = new WalkForwardConfig
{
    WindowSize = TimeSpan.FromDays(90),      // 90-day training window
    StepSize = TimeSpan.FromDays(30),        // Move forward 30 days each iteration
    ValidationSize = TimeSpan.FromDays(30),  // 30-day testing period
    Mode = WindowGenerationMode.Anchored     // Anchored: always start from beginning
                                             // Rolling: fixed window size that slides
};

// 2. Create validator
var validator = new WalkForwardValidator<MyStrategy>(optimizer, config);

// 3. Run walk-forward validation
var startDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
var endDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

var wfResult = validator.Validate(wfConfig, startDate, endDate);

// 4. Analyze walk-forward results
Console.WriteLine($"Total Windows: {wfResult.TotalWindows}");
Console.WriteLine($"Walk-Forward Efficiency: {wfResult.WalkForwardEfficiency:F2}");
Console.WriteLine($"Consistency: {wfResult.Consistency:F2}");

foreach (var window in wfResult.Windows)
{
    Console.WriteLine($"\nWindow {window.WindowNumber}:");
    Console.WriteLine($"  Training: {window.TrainingPeriod.start:yyyy-MM-dd} to {window.TrainingPeriod.end:yyyy-MM-dd}");
    Console.WriteLine($"  Testing: {window.TestingPeriod.start:yyyy-MM-dd} to {window.TestingPeriod.end:yyyy-MM-dd}");
    Console.WriteLine($"  Train Return: {window.TrainingMetrics.TotalReturn:F2}%");
    Console.WriteLine($"  Test Return: {window.TestingMetrics.TotalReturn:F2}%");
    Console.WriteLine($"  Degradation: {window.PerformanceDegradation:F2}%");
}
```

### Generating Interactive Reports

Generate HTML reports with candlestick charts, trade markers, and walk-forward analysis:

```csharp
using StockSharp.AdvancedBacktest.Export;

// 1. Prepare the web template (one-time setup)
// Navigate to StockSharp.AdvancedBacktest.Web and build the Next.js app:
// cd StockSharp.AdvancedBacktest.Web
// npm install
// npm run build

// 2. Select best strategy for visualization
var bestStrategy = results
    .OrderByDescending(r => r.Value.ValidationMetrics?.SharpeRatio ?? 0)
    .First();

// 3. Create chart model
var chartModel = new StrategySecurityChartModel
{
    Strategy = bestStrategy.Value.ValidatedStrategy ?? bestStrategy.Value.TrainedStrategy,
    HistoryPath = config.HistoryPath,
    OutputPath = @"C:\Reports\MyStrategyReport\index.html",
    WalkForwardResult = wfResult  // Optional: include walk-forward analysis
};

// 4. Generate report
var reportBuilder = new ReportBuilder<MyStrategy>();
await reportBuilder.GenerateReportAsync(chartModel, @"C:\Reports\MyStrategyReport");

Console.WriteLine("Report generated at: C:\\Reports\\MyStrategyReport\\index.html");
// Open in browser to view candlestick chart with trades and metrics
```

### Exporting Strategy Configurations for Live Trading

Export successful strategies for production deployment:

```csharp
using System.Text.Json;

// 1. Select strategies to export (e.g., top 3 by validation Sharpe)
var strategiesToExport = results
    .Where(r => r.Value.ValidationMetrics != null)
    .OrderByDescending(r => r.Value.ValidationMetrics.SharpeRatio)
    .Take(3)
    .ToList();

// 2. Create export directory
var exportDir = @"C:\Strategies\Production";
Directory.CreateDirectory(exportDir);

// 3. Export each strategy configuration
foreach (var (index, result) in strategiesToExport.Select((r, i) => (i + 1, r)))
{
    var strategy = result.Value.TrainedStrategy;

    // Extract parameter values
    var config = new Dictionary<string, object>
    {
        ["StrategyName"] = strategy.Name,
        ["Hash"] = result.Key,
        ["Parameters"] = strategy.ParamsBackup
            .ToDictionary(
                p => p.Id,
                p => p switch
                {
                    NumberParam n => (object)n.Value,
                    SecurityParam s => s.OptimizationRange.Keys.First().Id,
                    TimeSpanParam t => t.Value.ToString(),
                    _ => p.ToString()
                }
            ),
        ["Metrics"] = new
        {
            Training = new
            {
                result.Value.TrainingMetrics.TotalReturn,
                result.Value.TrainingMetrics.SharpeRatio,
                result.Value.TrainingMetrics.SortinoRatio,
                result.Value.TrainingMetrics.MaxDrawdown,
                result.Value.TrainingMetrics.WinRate,
                result.Value.TrainingMetrics.ProfitFactor
            },
            Validation = result.Value.ValidationMetrics != null ? new
            {
                result.Value.ValidationMetrics.TotalReturn,
                result.Value.ValidationMetrics.SharpeRatio,
                result.Value.ValidationMetrics.SortinoRatio,
                result.Value.ValidationMetrics.MaxDrawdown,
                result.Value.ValidationMetrics.WinRate,
                result.Value.ValidationMetrics.ProfitFactor
            } : null
        },
        ["TradingSettings"] = new
        {
            InitialCapital = config.InitialCapital,
            TradeVolume = config.TradeVolume,
            CommissionRules = config.CommissionRules.Select(r => r.GetType().Name).ToList()
        }
    };

    // Save as JSON
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    var filePath = Path.Combine(exportDir, $"strategy_{index}_{result.Key[..8]}.json");
    await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, jsonOptions));

    Console.WriteLine($"Exported: {filePath}");
    Console.WriteLine($"  Sharpe: {result.Value.ValidationMetrics?.SharpeRatio:F2}");
    Console.WriteLine($"  Return: {result.Value.ValidationMetrics?.TotalReturn:F2}%");
}
```

## Performance Metrics

The library calculates comprehensive performance metrics:

- **Returns**: Total Return, Annualized Return, Net/Gross Profit
- **Risk Metrics**: Sharpe Ratio, Sortino Ratio, Maximum Drawdown
- **Trade Statistics**: Win Rate, Profit Factor, Average Win/Loss
- **Activity**: Total Trades, Average Trades per Day

Access metrics via `PerformanceMetrics` class:

```csharp
var metrics = result.TrainingMetrics;
Console.WriteLine(metrics.ToDetailedString());
```

## Parameter Types

### Built-in Parameter Types

```csharp
// Numeric parameters (int, decimal, double)
new NumberParam("Period", canOptimize: true, start: 5, end: 50, step: 5);

// Security parameters with timeframes
new SecurityParam("Symbol", canOptimize: true)
{
    OptimizationRange = new Dictionary<Security, List<TimeSpan>>
    {
        { new Security { Id = "BTCUSDT@BNB" }, new() { TimeSpan.FromMinutes(5) } }
    }
};

// TimeSpan parameters
new TimeSpanParam("Timeframe", canOptimize: true,
    new[] { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1) });

// Class/Object parameters (custom strategies, indicators, etc.)
new ClassParam<IIndicator>("Indicator", canOptimize: true,
    new[] { new EMA(), new SMA(), new MACD() });
```

## Configuration Options

### OptimizationConfig

- **ParamsContainer**: Parameter definitions and validation rules
- **TrainingPeriod**: Dates for training and validation
- **HistoryPath**: Path to StockSharp historical data
- **InitialCapital**: Starting capital (default: 10000)
- **TradeVolume**: Volume per trade (default: 0.01)
- **ParallelWorkers**: CPU cores to use (default: all cores)
- **MetricFilters**: Functions to filter results by metrics
- **CommissionRules**: Trading commission configuration

### WalkForwardConfig

- **WindowSize**: Training window duration
- **StepSize**: How far to move forward each iteration
- **ValidationSize**: Testing period duration
- **Mode**: `Anchored` (expanding window) or `Rolling` (sliding window)

## Testing

Run the test suite:

```bash
dotnet test StockSharp.AdvancedBacktest.Tests/
```

## Web Visualization

Build the web interface (one-time setup):

```bash
cd StockSharp.AdvancedBacktest.Web
npm install
npm run build
```

The generated `out` folder contains the static HTML template used by `ReportBuilder`.

## Examples

See `StockSharp.AdvancedBacktest.Tests` for complete working examples:

- `WalkForwardIntegrationTests.cs`: Walk-forward validation examples
- `OptimizationLauncherWalkForwardTests.cs`: Full optimization pipeline
- `ReportBuilderIntegrationTests.cs`: Report generation examples

## License

This project extends [StockSharp](https://github.com/StockSharp/StockSharp), which is licensed under the GNU LGPL v3.
