using StockSharp.AdvancedBacktest.Backtest;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.ZigZagBreakout;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== ZigZag Breakout Strategy Backtest ===");
        Console.WriteLine();

        try
        {
            // Configuration
            const string historyPath = @"C:\Users\Andrew\OneDrive\Документы\StockSharp\Hydra\Storage";
            var startDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endDate = new DateTimeOffset(2023, 12, 31, 23, 59, 59, TimeSpan.Zero);
            const decimal initialCapital = 10000m;

            Console.WriteLine($"History Path: {historyPath}");
            Console.WriteLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"Initial Capital: {initialCapital:N2}");
            Console.WriteLine();

            // Create Security
            var security = new Security
            {
                Id = "BTCUSDT@BNB",
                Code = "BTCUSDT",
                Board = ExchangeBoard.Binance,
                PriceStep = 0.01m,  // BTCUSDT typically trades with 2 decimal places
                Decimals = 2
            };

            // Create Portfolio
            var portfolio = Portfolio.CreateSimulator();
            portfolio.BeginValue = initialCapital;
            portfolio.Name = "ZigZagBreakout";

            // Create Backtest Configuration
            var config = new BacktestConfig
            {
                ValidationPeriod = new PeriodConfig
                {
                    StartDate = startDate,
                    EndDate = endDate
                },
                HistoryPath = historyPath,
                MatchOnTouch = false
            };

            // Create Strategy Instance
            var strategy = new ZigZagBreakout
            {
                Security = security,
                Portfolio = portfolio
            };

            // Set timeframe - using 1 hour candles for this backtest
            strategy.Securities[security] = new[] { TimeSpan.FromHours(1) };

            // Set Strategy Parameters using CustomParams
            var parameters = new List<ICustomParam>
            {
                new NumberParam<decimal>("DzzDepth", 5m),
                new NumberParam<int>("JmaLength", 7),
                new NumberParam<int>("JmaPhase", 0),
                new NumberParam<int>("JmaUsage", -1)
            };
            strategy.ParamsContainer = new CustomParamsContainer(parameters);

            Console.WriteLine("Strategy Parameters:");
            Console.WriteLine($"  DzzDepth: 5");
            Console.WriteLine($"  JmaLength: 7");
            Console.WriteLine($"  JmaPhase: 0");
            Console.WriteLine($"  JmaUsage: -1 (Bearish trend filter)");
            Console.WriteLine();

            // Create and Run Backtest
            Console.WriteLine("Starting backtest...");
            Console.WriteLine();

            using var runner = new BacktestRunner<ZigZagBreakout>(config, strategy);
            var result = await runner.RunAsync();

            // Print Results
            Console.WriteLine();
            Console.WriteLine("=== Backtest Results ===");
            Console.WriteLine();

            if (result.IsSuccessful)
            {
                var metrics = result.Metrics;

                Console.WriteLine($"Status: SUCCESS");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2} seconds");
                Console.WriteLine();

                Console.WriteLine("Trading Performance:");
                Console.WriteLine($"  Total Trades: {metrics.TotalTrades}");
                Console.WriteLine($"  Winning Trades: {metrics.WinningTrades}");
                Console.WriteLine($"  Losing Trades: {metrics.LosingTrades}");
                Console.WriteLine($"  Win Rate: {metrics.WinRate:F1}%");
                Console.WriteLine();

                Console.WriteLine("Returns:");
                Console.WriteLine($"  Total Return: {metrics.TotalReturn:F2}%");
                Console.WriteLine($"  Annualized Return: {metrics.AnnualizedReturn:F2}%");
                Console.WriteLine($"  Net Profit: ${metrics.NetProfit:N2}");
                Console.WriteLine($"  Gross Profit: ${metrics.GrossProfit:N2}");
                Console.WriteLine($"  Gross Loss: ${metrics.GrossLoss:N2}");
                Console.WriteLine();

                Console.WriteLine("Risk Metrics:");
                Console.WriteLine($"  Sharpe Ratio: {metrics.SharpeRatio:F2}");
                Console.WriteLine($"  Sortino Ratio: {metrics.SortinoRatio:F2}");
                Console.WriteLine($"  Maximum Drawdown: {metrics.MaxDrawdown:F2}%");
                Console.WriteLine($"  Profit Factor: {metrics.ProfitFactor:F2}");
                Console.WriteLine();

                Console.WriteLine("Trade Analysis:");
                Console.WriteLine($"  Average Win: ${metrics.AverageWin:N2}");
                Console.WriteLine($"  Average Loss: ${metrics.AverageLoss:N2}");
                Console.WriteLine($"  Average Trades/Day: {metrics.AverageTradesPerDay:F2}");
                Console.WriteLine();

                Console.WriteLine("Capital:");
                Console.WriteLine($"  Initial Capital: ${metrics.InitialCapital:N2}");
                Console.WriteLine($"  Final Value: ${metrics.FinalValue:N2}");
                Console.WriteLine($"  Trading Period: {metrics.TradingPeriodDays} days");

                return 0;
            }
            else
            {
                Console.WriteLine($"Status: FAILED");
                Console.WriteLine($"Error: {result.ErrorMessage}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return 1;
        }
    }
}
