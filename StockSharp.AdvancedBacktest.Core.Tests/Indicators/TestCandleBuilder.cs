
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.Indicators;

public static class TestCandleBuilder
{
    private static readonly SecurityId DefaultSecurityId = new() { SecurityCode = "TEST", BoardCode = "TEST" };
    private static readonly TimeSpan DefaultTimeFrame = TimeSpan.FromMinutes(1);

    public static TimeFrameCandleMessage CreateCandle(
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        DateTime? openTime = null,
        CandleStates state = CandleStates.Finished)
    {
        var time = openTime ?? DateTime.UtcNow;

        return new TimeFrameCandleMessage
        {
            SecurityId = DefaultSecurityId,
            TypedArg = DefaultTimeFrame,
            OpenTime = time,
            CloseTime = time.Add(DefaultTimeFrame),
            OpenPrice = open,
            HighPrice = high,
            LowPrice = low,
            ClosePrice = close,
            TotalVolume = 1000,
            State = state
        };
    }

    public static TimeFrameCandleMessage CreateUpCandle(
        decimal open,
        decimal range,
        DateTime? openTime = null,
        CandleStates state = CandleStates.Finished)
    {
        var close = open + range;
        var high = close + range * 0.1m;
        var low = open - range * 0.1m;

        return CreateCandle(open, high, low, close, openTime, state);
    }

    public static TimeFrameCandleMessage CreateDownCandle(
        decimal open,
        decimal range,
        DateTime? openTime = null,
        CandleStates state = CandleStates.Finished)
    {
        var close = open - range;
        var low = close - range * 0.1m;
        var high = open + range * 0.1m;

        return CreateCandle(open, high, low, close, openTime, state);
    }

    public static TimeFrameCandleMessage CreateDojiCandle(
        decimal price,
        decimal wickSize,
        DateTime? openTime = null,
        CandleStates state = CandleStates.Finished)
    {
        return CreateCandle(price, price + wickSize, price - wickSize, price, openTime, state);
    }

    public static IEnumerable<TimeFrameCandleMessage> CreateCandleSequence(params (decimal open, decimal high, decimal low, decimal close)[] ohlcValues)
    {
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < ohlcValues.Length; i++)
        {
            var (open, high, low, close) = ohlcValues[i];
            yield return CreateCandle(open, high, low, close, startTime.AddMinutes(i));
        }
    }

    public static IEnumerable<TimeFrameCandleMessage> CreateUpTrendSequence(decimal startPrice, int count, decimal stepSize)
    {
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var price = startPrice;

        for (var i = 0; i < count; i++)
        {
            yield return CreateUpCandle(price, stepSize, startTime.AddMinutes(i));
            price += stepSize;
        }
    }

    public static IEnumerable<TimeFrameCandleMessage> CreateDownTrendSequence(decimal startPrice, int count, decimal stepSize)
    {
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var price = startPrice;

        for (var i = 0; i < count; i++)
        {
            yield return CreateDownCandle(price, stepSize, startTime.AddMinutes(i));
            price -= stepSize;
        }
    }
}
