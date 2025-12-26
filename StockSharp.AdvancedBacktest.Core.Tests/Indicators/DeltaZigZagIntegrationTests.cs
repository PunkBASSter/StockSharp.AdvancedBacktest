using StockSharp.AdvancedBacktest.Indicators;
using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Tests.Indicators;

public class DeltaZigZagIntegrationTests
{
    [Fact]
    public void OutputFormat_MatchesZigZagIndicatorValueExpectations()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),
            TestCandleBuilder.CreateCandle(104m, 120m, 103m, 118m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(118m, 119m, 105m, 106m, startTime.AddMinutes(2)),
        };

        // Act
        ZigZagIndicatorValue? result = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            result = (ZigZagIndicatorValue)indicator.Process(input);
        }

        // Assert - verify DeltaZigZagIndicatorValue structure (extends ZigZagIndicatorValue)
        Assert.NotNull(result);
        Assert.False(result.IsEmpty);
        Assert.True(result.IsUp); // Peak detected
        Assert.IsAssignableFrom<ZigZagIndicatorValue>(result); // DeltaZigZagIndicatorValue extends ZigZagIndicatorValue

        // Value should be accessible
        var value = result.GetValue<decimal>(null);
        Assert.True(value > 0);

        // Shift should be non-negative
        Assert.True(result.Shift >= 0);

        // Time should match input
        Assert.Equal(startTime.AddMinutes(2), result.Time);
    }

    [Fact]
    public void DeterministicResults_AcrossMultipleBacktestRuns()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),
            TestCandleBuilder.CreateCandle(104m, 115m, 103m, 112m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(112m, 125m, 111m, 123m, startTime.AddMinutes(2)),
            TestCandleBuilder.CreateCandle(123m, 124m, 105m, 108m, startTime.AddMinutes(3)),
            TestCandleBuilder.CreateCandle(108m, 109m, 90m, 92m, startTime.AddMinutes(4)),
            TestCandleBuilder.CreateCandle(92m, 110m, 91m, 108m, startTime.AddMinutes(5)),
        };

        // Act - Run multiple times with fresh indicators
        var results = new List<List<(bool isUp, decimal value, int shift)>>();

        for (var run = 0; run < 3; run++)
        {
            var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
            var runResults = new List<(bool isUp, decimal value, int shift)>();

            foreach (var candle in candles)
            {
                var input = new CandleIndicatorValue(indicator, candle);
                var result = (ZigZagIndicatorValue)indicator.Process(input);

                if (!result.IsEmpty)
                {
                    runResults.Add((result.IsUp, result.GetValue<decimal>(null), result.Shift));
                }
            }

            results.Add(runResults);
        }

        // Assert - all runs should produce identical results
        Assert.All(results, r => Assert.Equal(results[0].Count, r.Count));

        for (var i = 0; i < results[0].Count; i++)
        {
            var expected = results[0][i];
            Assert.All(results, r => Assert.Equal(expected, r[i]));
        }
    }

    [Fact]
    public void NumValuesToInitialize_ReturnsCorrectValue()
    {
        // Arrange
        var indicator = new DeltaZigZag();

        // Assert
        Assert.Equal(1, indicator.NumValuesToInitialize);
    }

    [Fact]
    public void CalcIsFormed_ReturnsCorrectFormationState()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Assert - not formed initially
        Assert.False(indicator.IsFormed);

        // Act - process first candle
        var candle = TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime);
        indicator.Process(new CandleIndicatorValue(indicator, candle));

        // Assert - formed after first candle (establishes initial direction)
        Assert.True(indicator.IsFormed);
    }

    [Fact]
    public void ToString_ReturnsReadableRepresentation()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };

        // Act
        var str = indicator.ToString();

        // Assert
        Assert.Contains("DeltaZigZag", str);
        Assert.Contains("Delta=0.5", str);
        Assert.Contains("MinThreshold=10", str);
    }

    [Fact]
    public void PeakAndTrough_ComplementaryFiltering()
    {
        // Arrange
        var peakIndicator = new DeltaZzPeak { Delta = 0.5m, MinimumThreshold = 5m };
        var troughIndicator = new DeltaZzTrough { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Create sequence with both peaks and troughs
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),       // Bar 0: Up
            TestCandleBuilder.CreateCandle(104m, 120m, 103m, 118m, startTime.AddMinutes(1)),  // Bar 1: Peak at 120
            TestCandleBuilder.CreateCandle(118m, 119m, 100m, 102m, startTime.AddMinutes(2)),  // Bar 2: Reversal down
            TestCandleBuilder.CreateCandle(102m, 103m, 90m, 92m, startTime.AddMinutes(3)),    // Bar 3: Trough at 90
            TestCandleBuilder.CreateCandle(92m, 110m, 91m, 108m, startTime.AddMinutes(4)),    // Bar 4: Reversal up
        };

        // Act
        var peaks = new List<decimal>();
        var troughs = new List<decimal>();

        foreach (var candle in candles)
        {
            var peakResult = (ZigZagIndicatorValue)peakIndicator.Process(new CandleIndicatorValue(peakIndicator, candle));
            var troughResult = (ZigZagIndicatorValue)troughIndicator.Process(new CandleIndicatorValue(troughIndicator, candle));

            if (!peakResult.IsEmpty)
                peaks.Add(peakResult.GetValue<decimal>(null));

            if (!troughResult.IsEmpty)
                troughs.Add(troughResult.GetValue<decimal>(null));
        }

        // Assert - peaks and troughs should be detected separately
        Assert.NotEmpty(peaks);
        Assert.NotEmpty(troughs);

        // The peak at 120 should be detected by peak indicator
        Assert.Contains(120m, peaks);

        // The trough at 90 should be detected by trough indicator
        Assert.Contains(90m, troughs);
    }

    [Fact]
    public void NoDoubleValuesOnSameTimestamp()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 120m, 99m, 118m, startTime),
            TestCandleBuilder.CreateCandle(118m, 119m, 90m, 92m, startTime.AddMinutes(1)),
        };

        // Act
        var resultsByTime = new Dictionary<DateTime, int>();
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = indicator.Process(input);

            if (!resultsByTime.TryGetValue(result.Time, out var count))
                count = 0;

            resultsByTime[result.Time] = count + 1;
        }

        // Assert - each timestamp should have exactly one result (empty or not)
        Assert.All(resultsByTime.Values, count => Assert.Equal(1, count));
    }
}
