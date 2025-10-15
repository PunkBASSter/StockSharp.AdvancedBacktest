using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.Algo.Commissions;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Models;

public class BacktestConfig
{
    public required PeriodConfig ValidationPeriod { get; set; }
    public required string HistoryPath { get; set; }
    public required CustomParamsContainer ParamsContainer { get; set; }

    /// <summary>
    /// Security identifier (e.g., "BTCUSDT@BNB")
    /// </summary>
    public required string SecurityId { get; set; }

    /// <summary>
    /// Market data type to use for backtesting (Ticks, Candles, MarketDepth, etc.)
    /// Use DataType.TimeFrame(timespan) for candles
    /// </summary>
    public DataType DataType { get; set; } = DataType.Ticks;

    /// <summary>
    /// Candle timeframe if using candles (e.g., TimeSpan.FromMinutes(5))
    /// </summary>
    public TimeSpan? CandleTimeFrame { get; set; }
    public string PortfolioName { get; set; } = "Simulator";

    /// <summary>
    /// Match order if historical price touched the limit order price
    /// False = more strict testing (price must go through the level)
    /// </summary>
    public bool MatchOnTouch { get; set; } = false;

    public decimal InitialCapital { get; set; } = 10000m;
    public IEnumerable<ICommissionRule> CommissionRules { get; set; } = [new CommissionTradeRule { Value = 0.1m }];
    public decimal TradeVolume { get; set; } = 0.01m;
}
