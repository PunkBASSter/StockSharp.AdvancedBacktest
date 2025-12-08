using StockSharp.AdvancedBacktest.Export;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;
using Microsoft.Extensions.Logging;

namespace StockSharp.AdvancedBacktest.Tests.Export;

public class BacktestExporterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithoutLogger_CreatesInstance()
    {
        // Act
        var exporter = new BacktestExporter();

        // Assert
        Assert.NotNull(exporter);
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // Arrange
        var logger = LoggerFactory.Create(builder => { }).CreateLogger<BacktestExporter>();

        // Act
        var exporter = new BacktestExporter(logger);

        // Assert
        Assert.NotNull(exporter);
    }

    #endregion

    #region ExtractSeries Tests (Backward Compatibility)

    [Fact]
    public void ExtractSeries_WithoutCandleInterval_ReturnsValidSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };

        // Process some candles
        for (int i = 0; i < 5; i++)
        {
            var candle = new TimeFrameCandleMessage
            {
                OpenTime = DateTime.UtcNow.AddHours(i),
                ClosePrice = 100m + i
            };
            indicator.Process(candle);
        }

        // Act
        var series = exporter.ExtractSeries(indicator);

        // Assert
        Assert.NotNull(series);
        Assert.Equal("SMA_3", series.Name);
        Assert.NotNull(series.Values);
        Assert.Equal("#2196F3", series.Color); // Blue for SMA
    }

    [Fact]
    public void ExtractSeries_WithCustomColor_UsesCustomColor()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };

        // Act
        var series = exporter.ExtractSeries(indicator, "#ABCDEF");

        // Assert
        Assert.Equal("#ABCDEF", series.Color);
    }

    [Fact]
    public void ExtractSeries_NullIndicator_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new BacktestExporter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.ExtractSeries(null!, null));
    }

    #endregion

    #region ExtractSeries Tests (With Candle Interval)

    [Fact]
    public void ExtractSeries_WithCandleInterval_ReturnsValidSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };
        var candleInterval = TimeSpan.FromHours(1);

        // Process some candles
        for (int i = 0; i < 5; i++)
        {
            var candle = new TimeFrameCandleMessage
            {
                OpenTime = DateTime.UtcNow.AddHours(i),
                ClosePrice = 100m + i
            };
            indicator.Process(candle);
        }

        // Act
        var series = exporter.ExtractSeries(indicator, candleInterval);

        // Assert
        Assert.NotNull(series);
        Assert.Equal("SMA_3", series.Name);
        Assert.NotNull(series.Values);
    }

    [Fact]
    public void ExtractSeries_WithCandleIntervalAndCustomColor_UsesCustomColor()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };
        var candleInterval = TimeSpan.FromHours(1);

        // Act
        var series = exporter.ExtractSeries(indicator, candleInterval, "#FEDCBA");

        // Assert
        Assert.Equal("#FEDCBA", series.Color);
    }

    #endregion

    #region ExtractComplexIndicator Tests (Backward Compatibility)

    [Fact]
    public void ExtractComplexIndicator_WithoutCandleInterval_ReturnsValidSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };

        // Act
        var seriesList = exporter.ExtractComplexIndicator(indicator);

        // Assert
        Assert.NotNull(seriesList);
        Assert.Single(seriesList);
        Assert.Equal("SMA_3", seriesList[0].Name);
    }

    [Fact]
    public void ExtractComplexIndicator_WithBollingerBands_ReturnsMultipleSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var bollingerBands = new BollingerBands { Length = 20, Width = 2 };

        var baseTime = DateTime.UtcNow;

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
        var seriesList = exporter.ExtractComplexIndicator(bollingerBands);

        // Assert
        Assert.NotNull(seriesList);
        Assert.True(seriesList.Count > 0, "Should extract at least one series from Bollinger Bands");

        // Verify all series have names
        foreach (var series in seriesList)
        {
            Assert.NotNull(series.Name);
            Assert.NotEmpty(series.Name);
        }
    }

    [Fact]
    public void ExtractComplexIndicator_NullIndicator_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new BacktestExporter();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.ExtractComplexIndicator(null!));
    }

    #endregion

    #region ExtractComplexIndicator Tests (With Candle Interval)

    [Fact]
    public void ExtractComplexIndicator_WithCandleInterval_ReturnsValidSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Length = 3, Name = "SMA_3" };
        var candleInterval = TimeSpan.FromHours(1);

        // Act
        var seriesList = exporter.ExtractComplexIndicator(indicator, candleInterval);

        // Assert
        Assert.NotNull(seriesList);
        Assert.Single(seriesList);
        Assert.Equal("SMA_3", seriesList[0].Name);
    }

    [Fact]
    public void ExtractComplexIndicator_WithCandleIntervalAndComplexIndicator_ReturnsMultipleSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var bollingerBands = new BollingerBands { Length = 20, Width = 2 };
        var candleInterval = TimeSpan.FromHours(1);

        var baseTime = DateTime.UtcNow;

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
        var seriesList = exporter.ExtractComplexIndicator(bollingerBands, candleInterval);

        // Assert
        Assert.NotNull(seriesList);
        Assert.True(seriesList.Count > 0);
    }

    [Fact]
    public void ExtractComplexIndicator_WithCandleIntervalNullIndicator_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var candleInterval = TimeSpan.FromHours(1);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.ExtractComplexIndicator(null!, candleInterval));
    }

    #endregion

    #region ExtractIndicators Tests (Batch Extraction)

    [Fact]
    public void ExtractIndicators_WithMultipleIndicators_ReturnsAllSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicators = new List<IIndicator>
        {
            new SimpleMovingAverage { Length = 3, Name = "SMA_3" },
            new ExponentialMovingAverage { Length = 5, Name = "EMA_5" }
        };
        var candleInterval = TimeSpan.FromHours(1);

        // Process some candles for each indicator
        foreach (var indicator in indicators)
        {
            for (int i = 0; i < 10; i++)
            {
                var candle = new TimeFrameCandleMessage
                {
                    OpenTime = DateTime.UtcNow.AddHours(i),
                    ClosePrice = 100m + i
                };
                indicator.Process(candle);
            }
        }

        // Act
        var allSeries = exporter.ExtractIndicators(indicators, candleInterval);

        // Assert
        Assert.NotNull(allSeries);
        Assert.Equal(2, allSeries.Count); // Two simple indicators = two series
        Assert.Contains(allSeries, s => s.Name == "SMA_3");
        Assert.Contains(allSeries, s => s.Name == "EMA_5");
    }

    [Fact]
    public void ExtractIndicators_WithComplexIndicator_FlattensSeries()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var bollingerBands = new BollingerBands { Length = 20, Width = 2 };
        var candleInterval = TimeSpan.FromHours(1);

        var baseTime = DateTime.UtcNow;

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
        var allSeries = exporter.ExtractIndicators(new[] { bollingerBands }, candleInterval);

        // Assert
        Assert.NotNull(allSeries);
        Assert.True(allSeries.Count > 0, "Should flatten complex indicator series");
    }

    [Fact]
    public void ExtractIndicators_NullIndicators_ThrowsArgumentNullException()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var candleInterval = TimeSpan.FromHours(1);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.ExtractIndicators(null!, candleInterval));
    }

    [Fact]
    public void ExtractIndicators_EmptyIndicators_ReturnsEmptyList()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var candleInterval = TimeSpan.FromHours(1);

        // Act
        var allSeries = exporter.ExtractIndicators(Enumerable.Empty<IIndicator>(), candleInterval);

        // Assert
        Assert.NotNull(allSeries);
        Assert.Empty(allSeries);
    }

    #endregion

    #region GetDefaultColor Tests

    [Fact]
    public void GetDefaultColor_SMA_ReturnsBlue()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new SimpleMovingAverage { Name = "SMA_20" };

        // Act
        var color = exporter.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#2196F3", color);
    }

    [Fact]
    public void GetDefaultColor_EMA_ReturnsOrange()
    {
        // Arrange
        var exporter = new BacktestExporter();
        var indicator = new ExponentialMovingAverage { Name = "EMA_50" };

        // Act
        var color = exporter.GetDefaultColor(indicator);

        // Assert
        Assert.Equal("#FF9800", color);
    }

    [Fact]
    public void GetDefaultColor_NullIndicator_ReturnsGrey()
    {
        // Arrange
        var exporter = new BacktestExporter();

        // Act
        var color = exporter.GetDefaultColor(null!);

        // Assert
        Assert.Equal("#607D8B", color);
    }

    #endregion
}
