using StockSharp.AdvancedBacktest.Indicators;
using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Tests.Indicators;

public class DeltaZzTroughTests
{
    [Fact]
    public void TroughOutput_WhenDeltaZigZagOutputsTrough_ReturnsValue()
    {
        // Arrange
        var indicator = new DeltaZzTrough { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Downtrend to trough, then reversal up
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 101m, 95m, 96m, startTime),
            TestCandleBuilder.CreateCandle(96m, 97m, 88m, 90m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(90m, 91m, 80m, 82m, startTime.AddMinutes(2)),
            TestCandleBuilder.CreateCandle(82m, 95m, 81m, 94m, startTime.AddMinutes(3)),
        };

        // Act
        ZigZagIndicatorValue? troughResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                troughResult = result;
        }

        // Assert
        Assert.NotNull(troughResult);
        Assert.False(troughResult.IsUp);
        Assert.Equal(80m, troughResult.GetValue<decimal>(null));
    }

    [Fact]
    public void EmptyOutput_WhenDeltaZigZagOutputsPeak_ReturnsEmpty()
    {
        // Arrange
        var indicator = new DeltaZzTrough { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Uptrend to peak, then reversal down
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),
            TestCandleBuilder.CreateCandle(104m, 112m, 103m, 110m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(110m, 120m, 109m, 118m, startTime.AddMinutes(2)),
            TestCandleBuilder.CreateCandle(118m, 119m, 105m, 106m, startTime.AddMinutes(3)),
        };

        // Act
        var results = new List<ZigZagIndicatorValue>();
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            results.Add(result);
        }

        // Assert - all results should be empty (peaks filtered out)
        var nonEmptyResults = results.Where(r => !r.IsEmpty).ToList();
        Assert.Empty(nonEmptyResults); // No troughs, only a peak which is filtered
    }

    [Fact]
    public void SingleValuePerTimestamp_NoDuplicates()
    {
        // Arrange
        var indicator = new DeltaZzTrough { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 101m, 95m, 96m, startTime),
            TestCandleBuilder.CreateCandle(96m, 97m, 80m, 82m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(82m, 95m, 81m, 94m, startTime.AddMinutes(2)),
        };

        // Act
        var results = new List<(DateTime time, bool isEmpty)>();
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = indicator.Process(input);
            results.Add((result.Time, result.IsEmpty));
        }

        // Assert - each timestamp should have exactly one output
        var groupedByTime = results.GroupBy(r => r.time);
        Assert.All(groupedByTime, g => Assert.Single(g));
    }

    [Fact]
    public void DeltaProperty_DelegatesToInternalDeltaZigZag()
    {
        // Arrange
        var indicator = new DeltaZzTrough();

        // Act
        indicator.Delta = 0.3m;

        // Assert
        Assert.Equal(0.3m, indicator.Delta);
    }

    [Fact]
    public void MinimumThresholdProperty_DelegatesToInternalDeltaZigZag()
    {
        // Arrange
        var indicator = new DeltaZzTrough();

        // Act
        indicator.MinimumThreshold = 20m;

        // Assert
        Assert.Equal(20m, indicator.MinimumThreshold);
    }

    [Fact]
    public void Reset_ClearsInternalState()
    {
        // Arrange
        var indicator = new DeltaZzTrough { Delta = 0.5m, MinimumThreshold = 10m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candle1 = TestCandleBuilder.CreateCandle(100m, 101m, 90m, 92m, startTime);
        var candle2 = TestCandleBuilder.CreateCandle(92m, 93m, 80m, 82m, startTime.AddMinutes(1));

        indicator.Process(new CandleIndicatorValue(indicator, candle1));
        indicator.Process(new CandleIndicatorValue(indicator, candle2));

        // Act
        indicator.Reset();

        // Process same candles again
        var result1 = indicator.Process(new CandleIndicatorValue(indicator, candle1));

        // Assert - after reset, first candle should return empty again
        Assert.True(result1.IsEmpty);
    }
}
