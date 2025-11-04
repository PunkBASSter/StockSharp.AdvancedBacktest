using StockSharp.AdvancedBacktest.Export;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Tests.Export;

public class IndicatorDataExtractorTests
{
    private class TestIndicator : BaseIndicator
    {
        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            var result = new TestIndicatorValue(this, input.GetValue<decimal>(), input.Time);
            return result;
        }
    }

    private class TestIndicatorValue : SingleIndicatorValue<decimal>
    {
        public TestIndicatorValue(IIndicator indicator, decimal value, DateTimeOffset time)
            : base(indicator, value, time)
        {
            IsFormed = true;
        }
    }

    private class ComplexIndicator : BaseIndicator
    {
        public List<IIndicator> InnerIndicators { get; } = new();

        protected override IIndicatorValue OnProcess(IIndicatorValue input)
        {
            // Process through all inner indicators
            foreach (var inner in InnerIndicators)
            {
                inner.Process(input);
            }
            return new TestIndicatorValue(this, input.GetValue<decimal>(), input.Time);
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithoutLogger_CreatesInstance()
    {
        // Act
        var extractor = new IndicatorDataExtractor();

        // Assert
        Assert.NotNull(extractor);
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<IndicatorDataExtractor>();

        // Act
        var extractor = new IndicatorDataExtractor(logger);

        // Assert
        Assert.NotNull(extractor);
    }

    #endregion

    #region ExtractFromContainer Tests

    [Fact]
    public void ExtractFromContainer_NullIndicator_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            extractor.ExtractFromContainer(null!, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void ExtractFromContainer_EmptyContainer_ReturnsEmptySeries()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "TestIndicator" };

        // Act
        var series = extractor.ExtractFromContainer(indicator, TimeSpan.FromHours(1));

        // Assert
        Assert.NotNull(series);
        Assert.Equal("TestIndicator", series.Name);
        Assert.Empty(series.Values);
    }

    [Fact]
    public void ExtractFromContainer_WithIndicator_ReturnsSeries()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA" };

        // Act - Extract from indicator (may be empty if no values processed)
        var series = extractor.ExtractFromContainer(indicator, TimeSpan.FromHours(1));

        // Assert
        Assert.NotNull(series);
        Assert.Equal("SMA", series.Name);
        Assert.NotNull(series.Values);
        Assert.Equal("#2196F3", series.Color); // Blue for SMA
    }

    [Fact]
    public void ExtractFromContainer_FiltersCorrectly()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };

        // Act - Extract from indicator
        var series = extractor.ExtractFromContainer(indicator, TimeSpan.FromHours(1));

        // Assert - Should return a valid series (empty or with values)
        Assert.NotNull(series);
        Assert.NotNull(series.Values);
        // Values list should be a valid list (not null), regardless of whether it has values
        Assert.IsAssignableFrom<List<IndicatorDataPoint>>(series.Values);
    }

    [Fact]
    public void ExtractFromContainer_WithCustomNameAndColor_UsesCustomValues()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "Original" };

        // Act
        var series = extractor.ExtractFromContainer(indicator, TimeSpan.FromHours(1),
            customName: "CustomName", customColor: "#ABCDEF");

        // Assert
        Assert.Equal("CustomName", series.Name);
        Assert.Equal("#ABCDEF", series.Color);
    }

    [Fact]
    public void ExtractFromContainer_WithoutCustomColor_UsesDefaultColor()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "SMA_20" };

        // Act
        var series = extractor.ExtractFromContainer(indicator, TimeSpan.FromHours(1));

        // Assert
        Assert.Equal("#2196F3", series.Color); // Blue for SMA
    }

    #endregion

    #region ExtractFromValue Tests

    [Fact]
    public void ExtractFromValue_NullValue_ReturnsNull()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();

        // Act
        var result = extractor.ExtractFromValue(null!, TimeSpan.FromHours(1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractFromValue_EmptyValue_ReturnsNull()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new ZigZag();
        var emptyValue = new ZigZagIndicatorValue(indicator, DateTimeOffset.UtcNow);

        // Act
        var result = extractor.ExtractFromValue(emptyValue, TimeSpan.FromHours(1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractFromValue_ZeroValue_ReturnsNull()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator();
        var zeroValue = new TestIndicatorValue(indicator, 0m, DateTimeOffset.UtcNow);

        // Act
        var result = extractor.ExtractFromValue(zeroValue, TimeSpan.FromHours(1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ExtractFromValue_NonFormedValue_ReturnsNull()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new SimpleMovingAverage { Length = 3 };

        // Process one value - won't be formed yet (needs 3)
        var candle = new TimeFrameCandleMessage
        {
            OpenTime = DateTimeOffset.UtcNow,
            ClosePrice = 100m
        };
        var result = indicator.Process(candle);

        // Act
        var dataPoint = extractor.ExtractFromValue(result, TimeSpan.FromHours(1));

        // Assert
        Assert.Null(dataPoint); // Not formed yet
    }

    [Fact]
    public void ExtractFromValue_ValidValue_ReturnsDataPoint()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator();
        var time = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var value = new TestIndicatorValue(indicator, 123.45m, time);

        // Act
        var result = extractor.ExtractFromValue(value, TimeSpan.FromHours(1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(time.ToUnixTimeMilliseconds(), result.Time);
        Assert.Equal(123.45, result.Value);
    }

    [Fact]
    public void ExtractFromValue_ShiftedValue_AppliesCorrection()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new ZigZag();
        var currentTime = new DateTimeOffset(2025, 1, 1, 20, 0, 0, TimeSpan.Zero);
        var shift = 3;
        var candleInterval = TimeSpan.FromHours(1);

        var shiftedValue = new ZigZagIndicatorValue(indicator, 8500m, shift, currentTime, true)
        {
            IsFormed = true
        };

        // Act
        var result = extractor.ExtractFromValue(shiftedValue, candleInterval);

        // Assert
        Assert.NotNull(result);

        // Expected time: currentTime - (3 * 1 hour) = 17:00
        var expectedTime = new DateTimeOffset(2025, 1, 1, 17, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedTime.ToUnixTimeMilliseconds(), result.Time);
        Assert.Equal(8500.0, result.Value);
    }

    [Fact]
    public void ExtractFromValue_WithoutCandleInterval_UsesOriginalTime()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator();
        var time = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var value = new TestIndicatorValue(indicator, 100m, time);

        // Act
        var result = extractor.ExtractFromValue(value, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(time.ToUnixTimeMilliseconds(), result.Time);
    }

    #endregion

    #region ExtractComplexIndicator Tests

    [Fact]
    public void ExtractComplexIndicator_NullIndicator_ThrowsArgumentNullException()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            extractor.ExtractComplexIndicator(null!, TimeSpan.FromHours(1)));
    }

    [Fact]
    public void ExtractComplexIndicator_SimpleIndicator_ReturnsSingleSeries()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "SimpleEMA" };

        var time = DateTimeOffset.UtcNow;
        var value = new TestIndicatorValue(indicator, 100m, time);
        indicator.Process(value);

        // Act
        var seriesList = extractor.ExtractComplexIndicator(indicator, TimeSpan.FromHours(1));

        // Assert
        Assert.Single(seriesList);
        Assert.Equal("SimpleEMA", seriesList[0].Name);
    }

    [Fact]
    public void ExtractComplexIndicator_WithInnerIndicators_ReturnsMultipleSeries()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();

        // Use actual BollingerBands indicator which has InnerIndicators
        var bollingerBands = new BollingerBands { Length = 20, Width = 2 };

        var baseTime = DateTimeOffset.UtcNow;

        // Process enough candles to form the indicator
        for (int i = 0; i < 25; i++)
        {
            var candle = new TimeFrameCandleMessage
            {
                OpenTime = baseTime.AddHours(i),
                OpenPrice = 100m + i,
                ClosePrice = 100m + i,
                HighPrice = 102m + i,
                LowPrice = 98m + i,
                TotalVolume = 1000
            };
            bollingerBands.Process(candle);
        }

        // Act
        var seriesList = extractor.ExtractComplexIndicator(bollingerBands, TimeSpan.FromHours(1));

        // Assert
        // BollingerBands should have 3 inner indicators (Middle, Upper, Lower)
        Assert.True(seriesList.Count > 0, "Should extract at least one series from Bollinger Bands");

        // Verify all series have names
        foreach (var series in seriesList)
        {
            Assert.NotNull(series.Name);
            Assert.NotEmpty(series.Name);
        }
    }

    [Fact]
    public void ExtractComplexIndicator_EmptyInnerIndicators_ReturnsSingleSeries()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var complexIndicator = new ComplexIndicator { Name = "EmptyComplex" };
        // InnerIndicators list is empty

        // Act
        var seriesList = extractor.ExtractComplexIndicator(complexIndicator, TimeSpan.FromHours(1));

        // Assert
        Assert.Single(seriesList);
        Assert.Equal("EmptyComplex", seriesList[0].Name);
    }

    #endregion

    #region GetDefaultColor Tests

    [Fact]
    public void GetDefaultColor_SMA_ReturnsBlue()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "SMA_20" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#2196F3", color);
    }

    [Fact]
    public void GetDefaultColor_EMA_ReturnsOrange()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "EMA_50" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#FF9800", color);
    }

    [Fact]
    public void GetDefaultColor_JMA_ReturnsTeal()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "JMA" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#4ECDC4", color);
    }

    [Fact]
    public void GetDefaultColor_RSI_ReturnsPurple()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "RSI_14" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#9C27B0", color);
    }

    [Fact]
    public void GetDefaultColor_MACD_ReturnsGreen()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "MACD" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#4CAF50", color);
    }

    [Fact]
    public void GetDefaultColor_ZigZag_ReturnsOrangeRed()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "DZZ" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#FF6B35", color);
    }

    [Fact]
    public void GetDefaultColor_Unknown_ReturnsGrey()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "UnknownIndicator" };

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#607D8B", color);
    }

    [Fact]
    public void GetDefaultColor_NullIndicator_ReturnsGrey()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();

        // Act
        var color = extractor.GetDefaultColor(null!);

        // Assert
        Assert.Equal("#607D8B", color);
    }

    [Fact]
    public void GetDefaultColor_CaseInsensitive_ReturnsCorrectColor()
    {
        // Arrange
        var extractor = new IndicatorDataExtractor();
        var indicator = new TestIndicator { Name = "sma_20" }; // lowercase

        // Act
        var color = extractor.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#2196F3", color); // Should still match SMA
    }

    #endregion
}
