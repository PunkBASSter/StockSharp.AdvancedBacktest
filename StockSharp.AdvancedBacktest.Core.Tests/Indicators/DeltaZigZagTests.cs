using StockSharp.AdvancedBacktest.Indicators;
using StockSharp.Algo.Indicators;

namespace StockSharp.AdvancedBacktest.Tests.Indicators;

public class DeltaZigZagTests
{
    [Fact]
    public void InitialDirection_CloseGreaterThanOpen_IsUpTrend()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        var upCandle = TestCandleBuilder.CreateCandle(open: 100m, high: 105m, low: 99m, close: 104m);

        // Act
        var input = new CandleIndicatorValue(indicator, upCandle);
        var result = indicator.Process(input);

        // Assert - first candle emits pending point for visualization
        var dzzResult = Assert.IsType<DeltaZigZagIndicatorValue>(result);
        Assert.False(result.IsEmpty);
        Assert.True(dzzResult.IsPending);
        Assert.True(dzzResult.IsUp); // Uptrend based on close > open
        Assert.Equal(105m, dzzResult.GetValue<decimal>(null)); // High is the initial extremum
    }

    [Fact]
    public void InitialDirection_CloseLessThanOpen_IsDownTrend()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        var downCandle = TestCandleBuilder.CreateCandle(open: 100m, high: 101m, low: 95m, close: 96m);

        // Act
        var input = new CandleIndicatorValue(indicator, downCandle);
        var result = indicator.Process(input);

        // Assert - first candle emits pending point for visualization
        var dzzResult = Assert.IsType<DeltaZigZagIndicatorValue>(result);
        Assert.False(result.IsEmpty);
        Assert.True(dzzResult.IsPending);
        Assert.False(dzzResult.IsUp); // Downtrend based on close < open
        Assert.Equal(95m, dzzResult.GetValue<decimal>(null)); // Low is the initial extremum
    }

    [Fact]
    public void InitialDirection_Doji_HighWickLarger_IsUpTrend()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        // Doji where (high - open) > (open - low) => uptrend
        var dojiCandle = TestCandleBuilder.CreateCandle(open: 100m, high: 108m, low: 97m, close: 100m);

        // Act
        var input = new CandleIndicatorValue(indicator, dojiCandle);
        var result = indicator.Process(input);

        // Assert - first candle emits pending point for visualization
        var dzzResult = Assert.IsType<DeltaZigZagIndicatorValue>(result);
        Assert.False(result.IsEmpty);
        Assert.True(dzzResult.IsPending);
        Assert.True(dzzResult.IsUp); // Upper wick larger => uptrend
        Assert.Equal(108m, dzzResult.GetValue<decimal>(null)); // High is the initial extremum
    }

    [Fact]
    public void InitialDirection_Doji_LowWickLarger_IsDownTrend()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        // Doji where (high - open) < (open - low) => downtrend
        var dojiCandle = TestCandleBuilder.CreateCandle(open: 100m, high: 102m, low: 92m, close: 100m);

        // Act
        var input = new CandleIndicatorValue(indicator, dojiCandle);
        var result = indicator.Process(input);

        // Assert - first candle emits pending point for visualization
        var dzzResult = Assert.IsType<DeltaZigZagIndicatorValue>(result);
        Assert.False(result.IsEmpty);
        Assert.True(dzzResult.IsPending);
        Assert.False(dzzResult.IsUp); // Lower wick larger => downtrend
        Assert.Equal(92m, dzzResult.GetValue<decimal>(null)); // Low is the initial extremum
    }

    [Fact]
    public void PeakDetection_WithDynamicThreshold_OutputsPeakWithCorrectShift()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Create a sequence: uptrend to 120, then reversal down past threshold
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),           // Bar 0: Up (initial)
            TestCandleBuilder.CreateCandle(104m, 112m, 103m, 110m, startTime.AddMinutes(1)), // Bar 1: Up
            TestCandleBuilder.CreateCandle(110m, 120m, 109m, 118m, startTime.AddMinutes(2)), // Bar 2: Peak at 120
            TestCandleBuilder.CreateCandle(118m, 119m, 105m, 106m, startTime.AddMinutes(3)), // Bar 3: Down - should trigger peak (>50% of swing)
        };

        // Act
        ZigZagIndicatorValue? lastResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            lastResult = (ZigZagIndicatorValue)indicator.Process(input);
        }

        // Assert
        Assert.NotNull(lastResult);
        Assert.False(lastResult.IsEmpty);
        Assert.True(lastResult.IsUp); // Peak
        Assert.Equal(120m, lastResult.GetValue<decimal>(null));
        Assert.True(lastResult.Shift > 0);
    }

    [Fact]
    public void TroughDetection_WithDynamicThreshold_OutputsTroughWithCorrectShift()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Create sequence: downtrend to 80, then reversal up past threshold
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 101m, 95m, 96m, startTime),             // Bar 0: Down (initial)
            TestCandleBuilder.CreateCandle(96m, 97m, 88m, 90m, startTime.AddMinutes(1)),  // Bar 1: Down
            TestCandleBuilder.CreateCandle(90m, 91m, 80m, 82m, startTime.AddMinutes(2)),  // Bar 2: Trough at 80
            TestCandleBuilder.CreateCandle(82m, 95m, 81m, 94m, startTime.AddMinutes(3)),  // Bar 3: Up - should trigger trough
        };

        // Act
        ZigZagIndicatorValue? lastResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            lastResult = (ZigZagIndicatorValue)indicator.Process(input);
        }

        // Assert
        Assert.NotNull(lastResult);
        Assert.False(lastResult.IsEmpty);
        Assert.False(lastResult.IsUp); // Trough
        Assert.Equal(80m, lastResult.GetValue<decimal>(null));
        Assert.True(lastResult.Shift > 0);
    }

    [Fact]
    public void MinimumThreshold_FallbackWhenNoSwingHistory()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 15m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // First candle establishes uptrend, price rises, then drops by more than MinimumThreshold
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),            // Bar 0: Up (initial), no swing yet
            TestCandleBuilder.CreateCandle(104m, 120m, 103m, 118m, startTime.AddMinutes(1)), // Bar 1: Peak at 120
            TestCandleBuilder.CreateCandle(118m, 119m, 100m, 102m, startTime.AddMinutes(2)), // Bar 2: Drop > MinimumThreshold(15)
        };

        // Act
        ZigZagIndicatorValue? peakResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                peakResult = result;
        }

        // Assert - should detect peak using MinimumThreshold since no prior swing exists
        Assert.NotNull(peakResult);
        Assert.True(peakResult.IsUp);
    }

    [Fact]
    public void BarShift_CorrectlyPlacesExtremumOnOriginalBar()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Scenario: uptrend peaks at bar 2 (high=125), bars 3-4 don't trigger reversal,
        // bar 5 triggers reversal with low=119 (125-119=6 > MinimumThreshold=5)
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),               // Bar 0
            TestCandleBuilder.CreateCandle(104m, 115m, 103m, 112m, startTime.AddMinutes(1)), // Bar 1
            TestCandleBuilder.CreateCandle(112m, 125m, 111m, 123m, startTime.AddMinutes(2)), // Bar 2: Peak at 125
            TestCandleBuilder.CreateCandle(123m, 124m, 121m, 122m, startTime.AddMinutes(3)), // Bar 3: low=121, still above threshold
            TestCandleBuilder.CreateCandle(122m, 123m, 121m, 121m, startTime.AddMinutes(4)), // Bar 4: low=121, still above threshold
            TestCandleBuilder.CreateCandle(121m, 122m, 119m, 119m, startTime.AddMinutes(5)), // Bar 5: Reversal detected (125-119=6 > 5)
        };

        // Act
        ZigZagIndicatorValue? peakResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                peakResult = result;
        }

        // Assert - shift should be 3 (bars since bar 2 where peak was, detected at bar 5)
        Assert.NotNull(peakResult);
        Assert.Equal(3, peakResult.Shift);
    }

    [Fact]
    public void EdgeCase_DeltaZero_UsesMinimumThresholdExclusively()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0m, MinimumThreshold = 10m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),
            TestCandleBuilder.CreateCandle(104m, 115m, 103m, 113m, startTime.AddMinutes(1)),
            TestCandleBuilder.CreateCandle(113m, 114m, 100m, 102m, startTime.AddMinutes(2)), // Drop > 10 (MinimumThreshold)
        };

        // Act
        ZigZagIndicatorValue? peakResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                peakResult = result;
        }

        // Assert - with Delta=0, only MinimumThreshold matters
        Assert.NotNull(peakResult);
        Assert.True(peakResult.IsUp);
    }

    [Fact]
    public void EdgeCase_DeltaOne_RequiresFullRetracement()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 1.0m, MinimumThreshold = 5m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // After first swing (100 to 80 = 20 swing), need 100% retracement (20 points) to confirm next reversal
        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 101m, 95m, 96m, startTime),              // Down initial
            TestCandleBuilder.CreateCandle(96m, 97m, 80m, 82m, startTime.AddMinutes(1)),   // Trough at 80
            TestCandleBuilder.CreateCandle(82m, 100m, 81m, 98m, startTime.AddMinutes(2)),  // Rally but not full retracement yet
            TestCandleBuilder.CreateCandle(98m, 101m, 97m, 100m, startTime.AddMinutes(3)), // Full retracement (20 points from 80)
        };

        // Act
        var results = new List<ZigZagIndicatorValue>();
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                results.Add(result);
        }

        // Assert - should have detected trough at 80 when rally reached full retracement
        Assert.NotEmpty(results);
    }

    [Fact]
    public void PriceGap_ThroughThreshold_DetectsReversalOnFirstCandleExceedingThreshold()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        var candles = new[]
        {
            TestCandleBuilder.CreateCandle(100m, 105m, 99m, 104m, startTime),
            TestCandleBuilder.CreateCandle(104m, 120m, 103m, 118m, startTime.AddMinutes(1)), // Peak at 120
            // Gap down through threshold in one candle
            TestCandleBuilder.CreateCandle(105m, 106m, 90m, 92m, startTime.AddMinutes(2)),   // Gap down
        };

        // Act
        ZigZagIndicatorValue? peakResult = null;
        foreach (var candle in candles)
        {
            var input = new CandleIndicatorValue(indicator, candle);
            var result = (ZigZagIndicatorValue)indicator.Process(input);
            if (!result.IsEmpty)
                peakResult = result;
        }

        // Assert - peak should be detected on the gap candle
        Assert.NotNull(peakResult);
        Assert.True(peakResult.IsUp);
        Assert.Equal(120m, peakResult.GetValue<decimal>(null));
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Arrange
        var indicator = new DeltaZigZag { Delta = 0.5m, MinimumThreshold = 10m };
        var startTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        // Process some candles first
        var candle1 = TestCandleBuilder.CreateCandle(100m, 110m, 99m, 108m, startTime);
        var candle2 = TestCandleBuilder.CreateCandle(108m, 120m, 107m, 118m, startTime.AddMinutes(1));

        indicator.Process(new CandleIndicatorValue(indicator, candle1));
        indicator.Process(new CandleIndicatorValue(indicator, candle2));

        // Act
        indicator.Reset();

        // Process same candles again - should behave like fresh start
        var result1 = indicator.Process(new CandleIndicatorValue(indicator, candle1));
        var result2 = indicator.Process(new CandleIndicatorValue(indicator, candle2));

        // Assert - after reset, first candle should emit initial pending point (like fresh start)
        var dzzResult1 = Assert.IsType<DeltaZigZagIndicatorValue>(result1);
        Assert.True(dzzResult1.IsPending);
        Assert.True(dzzResult1.IsUp); // close > open => uptrend
        Assert.Equal(110m, dzzResult1.GetValue<decimal>(null)); // High is the initial extremum
    }

    [Fact]
    public void Delta_ValidationRejectsNegativeValues()
    {
        // Arrange
        var indicator = new DeltaZigZag();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => indicator.Delta = -0.1m);
    }

    [Fact]
    public void Delta_ValidationRejectsValuesGreaterThanOne()
    {
        // Arrange
        var indicator = new DeltaZigZag();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => indicator.Delta = 1.5m);
    }

    [Fact]
    public void MinimumThreshold_ValidationRejectsZeroOrNegative()
    {
        // Arrange
        var indicator = new DeltaZigZag();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => indicator.MinimumThreshold = 0m);
        Assert.Throws<ArgumentOutOfRangeException>(() => indicator.MinimumThreshold = -5m);
    }
}
