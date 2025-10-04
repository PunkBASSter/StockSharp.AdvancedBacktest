using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Strategies;

namespace StockSharp.AdvancedBacktest.Export;

public class ReportBuilder<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    public void GenerateInteractiveChart(StrategySecurityChartModel model, bool openInBrowser = false)
    {
        var chartData = new ChartDataModel();
        chartData.Candles = ExtractCandleData(model);
        chartData.Trades = ExtractTradeData(model.Strategy);
        var htmlContent = GenerateChartHtml(chartData);
        var outputDir = Path.GetDirectoryName(model.OutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);
        File.WriteAllText(model.OutputPath, htmlContent, System.Text.Encoding.UTF8);

        if (openInBrowser)
        {
            var browserPath = Environment.GetEnvironmentVariable("BROWSER_PATH") ?? "chrome";
            var filePath = Path.GetFullPath(model.OutputPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = filePath,
                UseShellExecute = true
            });
        }
    }

    private List<CandleDataPoint> ExtractCandleData(StrategySecurityChartModel model)
    {
        var historyPath = model.HistoryPath;
        var securities = model.Strategy.Securities;
        using var dataDrive = new LocalMarketDataDrive(historyPath);
        using var tempRegistry = new StorageRegistry { DefaultDrive = dataDrive };
        var candles = new List<CandleDataPoint>();
        var security = model.Strategy.Security ?? securities.Keys.FirstOrDefault();
        if (security == null)
            return candles;
        //foreach (var security in securities.Keys)
        {
            var lowestTimeFrame = securities[security].FirstOrDefault();
            var securityId = security.Id.ToSecurityId();
            var candleStorage = tempRegistry.GetCandleMessageStorage(
                typeof(TimeFrameCandleMessage),
                securityId,
                lowestTimeFrame,
                format: StorageFormats.Binary);

            var dates = candleStorage.Dates.ToArray();

            candles = dates.SelectMany(date => candleStorage.Load(date))
                .Select(c => new CandleDataPoint
                {
                    Time = c.OpenTime.ToUnixTimeSeconds(),
                    Open = (double)c.OpenPrice,
                    High = (double)c.HighPrice,
                    Low = (double)c.LowPrice,
                    Close = (double)c.ClosePrice,
                    Volume = (double)c.TotalVolume
                })
                .OrderBy(c => c.Time)
                .ToList();
        }
        return candles;
    }

    private List<TradeDataPoint> ExtractTradeData(Strategy strategy)
    {
        var trades = new List<TradeDataPoint>();

        foreach (var myTrade in strategy.MyTrades)
        {
            // Determine trade side from order side or volume sign
            var side = "buy"; // Default
            if (myTrade.Order?.Side != null)
            {
                side = myTrade.Order.Side == Sides.Buy ? "buy" : "sell";
            }
            else if (myTrade.Trade.Volume != 0)
            {
                // If no order side, infer from volume (positive = buy, negative = sell)
                side = myTrade.Trade.Volume > 0 ? "buy" : "sell";
            }

            trades.Add(new TradeDataPoint
            {
                Time = ((DateTimeOffset)myTrade.Trade.ServerTime).ToUnixTimeSeconds(),
                Price = (double)myTrade.Trade.Price,
                Volume = (double)Math.Abs(myTrade.Trade.Volume),
                Side = side,
                PnL = (double)(myTrade.PnL ?? 0)
            });
        }

        return trades.OrderBy(t => t.Time).ToList();
    }

    /// <summary>
    /// Get appropriate color for indicator based on type
    /// </summary>
    private static string GetIndicatorColor(IIndicator indicator)
    {
        // Simple color assignment based on indicator type
        return indicator.Name.ToLower() switch
        {
            var name when name.Contains("sma") || name.Contains("simple") => "#2196F3",
            var name when name.Contains("ema") || name.Contains("exponential") => "#FF9800",
            var name when name.Contains("rsi") => "#9C27B0",
            var name when name.Contains("macd") => "#4CAF50",
            var name when name.Contains("bollinger") => "#F44336",
            _ => "#607D8B"
        };
    }

    /// <summary>
    /// Generate HTML content with TradingView chart
    /// </summary>
    private string GenerateChartHtml(ChartDataModel chartData)
    {
        var template = GetHtmlTemplate();
        var chartDataJson = JsonSerializer.Serialize(chartData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        return template.Replace("{{ CHART_DATA }}", chartDataJson);
    }

    /// <summary>
    /// Get HTML template with TradingView Lightweight Charts
    /// </summary>
    private static string GetHtmlTemplate()
    {
        return File.Exists("chart-template.html")
            ? File.ReadAllText("chart-template.html")
            : throw new FileNotFoundException("chart-template.html not found");
    }
}
