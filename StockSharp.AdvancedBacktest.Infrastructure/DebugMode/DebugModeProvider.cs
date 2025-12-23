using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

/// <summary>
/// Unified debug mode provider that manages both AI and human debug outputs.
/// Filters out auxiliary timeframe events from all outputs.
/// </summary>
public class DebugModeProvider : IDisposable
{
    private IDebugModeOutput? _humanOutput;
    private IDebugModeOutput? _aiOutput;
    private bool _disposed;

    public bool IsHumanDebugEnabled { get; set; }
    public bool IsAiDebugEnabled { get; set; }
    public TimeSpan MainTimeframe { get; set; } = TimeSpan.FromHours(1);

    public void SetHumanOutput(IDebugModeOutput output)
    {
        _humanOutput = output;
    }

    public void SetAiOutput(IDebugModeOutput output)
    {
        _aiOutput = output;
    }

    public void CaptureEvent(object eventData, DateTimeOffset timestamp, bool isAuxiliaryTimeframe)
    {
        // Filter out auxiliary TF events completely
        if (isAuxiliaryTimeframe)
            return;

        // Remap timestamp to main TF boundary
        var displayTimestamp = TimestampRemapper.RemapToMainTimeframe(timestamp, MainTimeframe);

        if (IsHumanDebugEnabled && _humanOutput != null)
        {
            _humanOutput.Write(eventData, displayTimestamp);
        }

        if (IsAiDebugEnabled && _aiOutput != null)
        {
            _aiOutput.Write(eventData, displayTimestamp);
        }
    }

    public void CaptureCandle(ICandleMessage candle, string securityId, bool isAuxiliaryTimeframe)
    {
        if (isAuxiliaryTimeframe)
            return;

        var candleEvent = new
        {
            Type = "Candle",
            SecurityId = securityId,
            OpenTime = candle.OpenTime,
            Open = candle.OpenPrice,
            High = candle.HighPrice,
            Low = candle.LowPrice,
            Close = candle.ClosePrice,
            Volume = candle.TotalVolume
        };

        var displayTimestamp = TimestampRemapper.RemapToMainTimeframe(candle.OpenTime, MainTimeframe);

        if (IsHumanDebugEnabled && _humanOutput != null)
        {
            _humanOutput.Write(candleEvent, displayTimestamp);
        }

        if (IsAiDebugEnabled && _aiOutput != null)
        {
            _aiOutput.Write(candleEvent, displayTimestamp);
        }
    }

    public void CaptureIndicator(string indicatorName, decimal? value, DateTimeOffset timestamp, bool isAuxiliaryTimeframe)
    {
        if (isAuxiliaryTimeframe)
            return;

        var indicatorEvent = new
        {
            Type = "Indicator",
            Name = indicatorName,
            Value = value,
            Timestamp = timestamp
        };

        CaptureEvent(indicatorEvent, timestamp, isAuxiliaryTimeframe: false);
    }

    public void CaptureTrade(MyTrade trade, bool isAuxiliaryTimeframe)
    {
        if (isAuxiliaryTimeframe)
            return;

        var tradeTime = trade.Trade?.ServerTime ?? DateTime.UtcNow;
        var tradeEvent = new
        {
            Type = "Trade",
            OrderId = trade.Order?.Id,
            TradeId = trade.Trade?.Id,
            Price = trade.Trade?.Price,
            Volume = trade.Trade?.Volume,
            Side = trade.Order?.Side.ToString(),
            Timestamp = tradeTime
        };

        CaptureEvent(tradeEvent, new DateTimeOffset(tradeTime), isAuxiliaryTimeframe: false);
    }

    public void Flush()
    {
        _humanOutput?.Flush();
        _aiOutput?.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _humanOutput?.Dispose();
        _aiOutput?.Dispose();
        _disposed = true;
    }
}
