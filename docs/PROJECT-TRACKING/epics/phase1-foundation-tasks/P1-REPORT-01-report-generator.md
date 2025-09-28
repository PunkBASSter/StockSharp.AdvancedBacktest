# Task: P1-REPORT-01 - Implement ReportGenerator MVP

**Epic**: Phase1-Foundation
**Priority**: MEDIUM-01
**Agent**: dotnet-csharp-expert
**Status**: READY
**Dependencies**: P1-DATA-02, P1-PERF-01

## Overview

Implement a basic ReportGenerator that creates self-contained HTML reports with interactive visualizations using Next.js templates and Chart.js for data visualization. The generator produces static reports that can be viewed without a web server and exported for sharing.

## Technical Requirements

### Core Implementation

1. **ReportGenerator Class**
   - Generate self-contained HTML reports from optimization artifacts
   - Integrate with Next.js templates for modern UI
   - Embed interactive charts using Chart.js for data visualization
   - Support multiple report formats (summary, detailed, comparison)
   - Create portable reports that work offline

2. **Key Components to Implement**
   ```csharp
   public class ReportGenerator
   {
       public async Task<ReportGenerationResult> GenerateReportAsync(ReportConfiguration config, ArtifactPath artifactPath);
       public async Task<ReportGenerationResult> GenerateComparisonReportAsync(List<ArtifactPath> artifactPaths);
       public async Task<byte[]> ExportToPdfAsync(string htmlReportPath);
       public ReportTemplate GetTemplate(ReportType reportType);
   }
   ```

3. **Report Templates System**
   ```csharp
   public abstract class ReportTemplate
   {
       public abstract string TemplateName { get; }
       public abstract Task<string> RenderAsync(ReportData data);
       protected virtual Dictionary<string, object> GetTemplateVariables(ReportData data);
   }
   ```

### File Structure

Create in `StockSharp.AdvancedBacktest/Infrastructure/Reporting/`:
- `ReportGenerator.cs` - Main report generator class
- `ReportConfiguration.cs` - Report configuration model
- `ReportData.cs` - Data model for report rendering
- `Templates/SummaryReportTemplate.cs` - Summary report template
- `Templates/DetailedReportTemplate.cs` - Detailed report template
- `Templates/ComparisonReportTemplate.cs` - Multi-strategy comparison template
- `Charts/ChartGenerator.cs` - Chart.js integration
- `Assets/` - Static assets (CSS, JS, fonts)

## Implementation Details

### Report Generation Architecture

1. **Data Extraction and Preparation**
   ```csharp
   public class ReportData
   {
       public StrategyMetadata Strategy { get; set; }
       public PerformanceMetrics Performance { get; set; }
       public List<Trade> Trades { get; set; }
       public List<ParameterCombinationResult> OptimizationResults { get; set; }
       public List<ChartData> Charts { get; set; }
       public ReportStatistics Statistics { get; set; }
       public DateTime GeneratedAt { get; set; }
   }

   private async Task<ReportData> ExtractReportDataAsync(ArtifactPath artifactPath)
   {
       var strategy = await _artifactManager.RetrieveArtifactAsync<StrategyConfiguration>(artifactPath, "strategy-config.json");
       var performance = await _artifactManager.RetrieveArtifactAsync<PerformanceMetrics>(artifactPath, "performance-metrics.json");
       var trades = await LoadTradesAsync(artifactPath);
       var optimizationResults = await _artifactManager.RetrieveArtifactAsync<List<ParameterCombinationResult>>(artifactPath, "optimization-results.json");

       return new ReportData
       {
           Strategy = strategy.Metadata,
           Performance = performance,
           Trades = trades,
           OptimizationResults = optimizationResults,
           Charts = await GenerateChartsAsync(performance, trades),
           Statistics = CalculateReportStatistics(performance, trades),
           GeneratedAt = DateTime.UtcNow
       };
   }
   ```

2. **Template System**
   ```csharp
   public class SummaryReportTemplate : ReportTemplate
   {
       public override string TemplateName => "Summary";

       public override async Task<string> RenderAsync(ReportData data)
       {
           var templatePath = Path.Combine("Templates", "summary-template.html");
           var template = await File.ReadAllTextAsync(templatePath);

           var variables = GetTemplateVariables(data);
           return ReplaceTemplateVariables(template, variables);
       }

       protected override Dictionary<string, object> GetTemplateVariables(ReportData data)
       {
           return new Dictionary<string, object>
           {
               ["strategyName"] = data.Strategy.Name,
               ["totalReturn"] = data.Performance.TotalReturn.ToString("P2"),
               ["sharpeRatio"] = data.Performance.SharpeRatio.ToString("F2"),
               ["maxDrawdown"] = data.Performance.MaxDrawdown.ToString("P2"),
               ["totalTrades"] = data.Trades.Count,
               ["winRate"] = data.Performance.TradeStats.WinRate.ToString("P1"),
               ["generatedAt"] = data.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
               ["performanceChart"] = GeneratePerformanceChartJson(data.Performance),
               ["drawdownChart"] = GenerateDrawdownChartJson(data.Performance),
               ["tradeDistributionChart"] = GenerateTradeDistributionChartJson(data.Trades)
           };
       }
   }
   ```

### Chart Generation

1. **Chart.js Integration**
   ```csharp
   public class ChartGenerator
   {
       public ChartData GeneratePerformanceChart(PerformanceMetrics performance)
       {
           return new ChartData
           {
               Type = "line",
               Data = new
               {
                   labels = performance.DailyReturns.Select((_, i) => $"Day {i + 1}").ToArray(),
                   datasets = new[]
                   {
                       new
                       {
                           label = "Cumulative Return",
                           data = CalculateCumulativeReturns(performance.DailyReturns),
                           borderColor = "rgb(75, 192, 192)",
                           backgroundColor = "rgba(75, 192, 192, 0.2)",
                           tension = 0.1
                       }
                   }
               },
               Options = new
               {
                   responsive = true,
                   plugins = new
                   {
                       title = new
                       {
                           display = true,
                           text = "Strategy Performance Over Time"
                       }
                   },
                   scales = new
                   {
                       y = new
                       {
                           beginAtZero = false,
                           title = new
                           {
                               display = true,
                               text = "Cumulative Return"
                           }
                       }
                   }
               }
           };
       }

       public ChartData GenerateDrawdownChart(DrawdownAnalysis drawdownStats)
       {
           return new ChartData
           {
               Type = "area",
               Data = new
               {
                   labels = drawdownStats.DrawdownPeriods.Select(d => d.StartDate.ToString("yyyy-MM-dd")).ToArray(),
                   datasets = new[]
                   {
                       new
                       {
                           label = "Drawdown",
                           data = drawdownStats.DrawdownPeriods.Select(d => -d.DrawdownPercentage).ToArray(),
                           borderColor = "rgb(255, 99, 132)",
                           backgroundColor = "rgba(255, 99, 132, 0.2)",
                           fill = true
                       }
                   }
               }
           };
       }
   }
   ```

2. **Interactive Features**
   ```html
   <!-- Embedded in template -->
   <script>
   function initializeCharts(chartData) {
       // Performance Chart
       const performanceCtx = document.getElementById('performanceChart').getContext('2d');
       new Chart(performanceCtx, chartData.performanceChart);

       // Drawdown Chart
       const drawdownCtx = document.getElementById('drawdownChart').getContext('2d');
       new Chart(drawdownCtx, chartData.drawdownChart);

       // Trade Distribution Chart
       const distributionCtx = document.getElementById('distributionChart').getContext('2d');
       new Chart(distributionCtx, chartData.tradeDistributionChart);
   }

   // Initialize charts when page loads
   document.addEventListener('DOMContentLoaded', function() {
       initializeCharts(window.reportChartData);
   });
   </script>
   ```

### Template Structure

1. **HTML Template Example**
   ```html
   <!DOCTYPE html>
   <html lang="en">
   <head>
       <meta charset="UTF-8">
       <meta name="viewport" content="width=device-width, initial-scale=1.0">
       <title>{{strategyName}} - Backtest Report</title>
       <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
       <style>
           /* Embedded CSS for offline functionality */
           body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
           .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
           .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; }
           .metric-card { background: #f8f9fa; padding: 20px; border-radius: 8px; border-left: 4px solid #007bff; }
           .chart-container { margin: 20px 0; height: 400px; }
           /* ... more styles ... */
       </style>
   </head>
   <body>
       <div class="container">
           <header>
               <h1>{{strategyName}} Backtest Report</h1>
               <p>Generated: {{generatedAt}}</p>
           </header>

           <section class="performance-summary">
               <h2>Performance Summary</h2>
               <div class="metrics-grid">
                   <div class="metric-card">
                       <h3>Total Return</h3>
                       <div class="metric-value">{{totalReturn}}</div>
                   </div>
                   <div class="metric-card">
                       <h3>Sharpe Ratio</h3>
                       <div class="metric-value">{{sharpeRatio}}</div>
                   </div>
                   <!-- ... more metrics ... -->
               </div>
           </section>

           <section class="charts">
               <h2>Performance Analysis</h2>
               <div class="chart-container">
                   <canvas id="performanceChart"></canvas>
               </div>
               <div class="chart-container">
                   <canvas id="drawdownChart"></canvas>
               </div>
           </section>
       </div>

       <script>
           window.reportChartData = {{{chartDataJson}}};
       </script>
       <script src="report-charts.js"></script>
   </body>
   </html>
   ```

## Acceptance Criteria

### Functional Requirements

- [ ] Generates self-contained HTML reports that work offline
- [ ] Creates interactive charts for performance visualization
- [ ] Supports multiple report templates (summary, detailed)
- [ ] Exports reports from optimization artifacts
- [ ] Includes all key performance metrics and statistics

### Quality Requirements

- [ ] Reports render correctly in all major browsers
- [ ] Charts are interactive and responsive
- [ ] File size under 5MB for typical reports
- [ ] Generation time under 30 seconds for standard reports
- [ ] Professional appearance suitable for client presentations

### Technical Requirements

- [ ] Uses modern HTML5/CSS3 standards
- [ ] Embeds all dependencies for offline functionality
- [ ] Properly handles large datasets (10,000+ trades)
- [ ] Supports custom branding and styling
- [ ] Compatible with PDF export

## Implementation Specifications

### Report Configuration

```csharp
public class ReportConfiguration
{
    public ReportType Type { get; set; } = ReportType.Summary;
    public string OutputPath { get; set; }
    public bool IncludeCharts { get; set; } = true;
    public bool IncludeTradeDetails { get; set; } = false;
    public string CustomTitle { get; set; }
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}

public enum ReportType
{
    Summary,
    Detailed,
    Comparison,
    Custom
}
```

### Performance Optimization

1. **Large Dataset Handling**
   - Pagination for trade tables
   - Chart data sampling for large datasets
   - Lazy loading of non-critical sections

2. **Asset Management**
   - Embedded CSS and JavaScript
   - Optimized images and fonts
   - Minified resources for smaller file sizes

## Dependencies

### NuGet Packages Required

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.46" />
```

### External Dependencies

- Chart.js (embedded via CDN or local copy)
- Modern web browser for viewing reports

## Definition of Done

1. **Code Complete**
   - ReportGenerator fully implemented
   - Template system working
   - Chart generation functional
   - Asset management complete

2. **Testing Complete**
   - Unit tests for report generation
   - Template rendering tests
   - Chart generation validation
   - Cross-browser compatibility testing

3. **Documentation Complete**
   - XML documentation for all APIs
   - Template customization guide
   - Chart configuration documentation
   - Usage examples

4. **Integration Verified**
   - Works with artifact management system
   - Integrates with performance calculator
   - Generates professional-quality reports
   - Supports all required chart types

## Implementation Notes

### Design Considerations

1. **Offline Functionality**: All dependencies must be embedded
2. **Performance**: Optimize for large datasets and fast generation
3. **Maintainability**: Clear separation between data, templates, and rendering
4. **Extensibility**: Easy to add new chart types and templates

### Common Pitfalls to Avoid

1. External dependencies that break offline functionality
2. Memory issues with large datasets
3. Poor chart performance with many data points
4. Template injection vulnerabilities

This task creates professional-quality reports that showcase the results of the advanced backtesting system and provide actionable insights for strategy evaluation.