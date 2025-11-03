using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.BusinessEntities;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.DebugMode;

public class DebugModeExporterTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<string> _filesToCleanup = new();

    public DebugModeExporterTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"DebugModeExporterTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        foreach (var file in _filesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch { }
        }

        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch { }
    }

    private string GetTestFilePath(string filename = "test.jsonl")
    {
        var path = Path.Combine(_testDirectory, filename);
        _filesToCleanup.Add(path);

        // Also add potential rotation files
        for (int i = 1; i <= 10; i++)
        {
            var baseName = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var rotatedPath = Path.Combine(_testDirectory, $"{baseName}_{i:D3}{ext}");
            _filesToCleanup.Add(rotatedPath);
        }

        return path;
    }

    private class TestStrategy : CustomStrategyBase
    {
    }

    private class TestIndicator : StockSharp.Algo.Indicators.BaseIndicator
    {
        protected override StockSharp.Algo.Indicators.IIndicatorValue OnProcess(StockSharp.Algo.Indicators.IIndicatorValue input)
        {
            return input;
        }
    }

    private class TestIndicatorValue : StockSharp.Algo.Indicators.SingleIndicatorValue<decimal>
    {
        public TestIndicatorValue(StockSharp.Algo.Indicators.IIndicator indicator, decimal value, DateTimeOffset time)
            : base(indicator, value, time)
        {
        }
    }

    #region Lifecycle Tests

    [Fact]
    public void Initialize_CreatesBufferAndWriter()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        // Act
        exporter.Initialize(strategy);

        // Assert
        Assert.True(exporter.IsInitialized);
        Assert.Equal(filePath, exporter.OutputPath);
        Assert.Equal(0, exporter.EventCount);
        Assert.Equal(0, exporter.CurrentSequence);

        exporter.Cleanup();
    }

    [Fact]
    public void Initialize_WithNullStrategy_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.Initialize(null!));
    }

    [Fact]
    public void Cleanup_DisposesResources()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);
        Assert.True(exporter.IsInitialized);

        // Act
        exporter.Cleanup();

        // Assert
        Assert.False(exporter.IsInitialized);
    }

    [Fact]
    public void Cleanup_WithoutInitialize_DoesNotThrow()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);

        // Act & Assert - Should not throw
        exporter.Cleanup();
    }

    [Fact]
    public void Initialize_MultipleTimes_ResetsState()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        // Act
        exporter.Initialize(strategy);
        exporter.CaptureCandle(new CandleDataPoint { Time = 1 });
        var firstSequence = exporter.CurrentSequence;

        exporter.Cleanup();
        exporter.Initialize(strategy);

        // Assert
        Assert.Equal(0, exporter.CurrentSequence); // Should reset
        Assert.True(exporter.IsInitialized);

        exporter.Cleanup();
    }

    [Fact]
    public void Dispose_CallsCleanup()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);
        Assert.True(exporter.IsInitialized);

        // Act
        exporter.Dispose();

        // Assert
        Assert.False(exporter.IsInitialized);
    }

    [Fact]
    public void Dispose_MultipleCalls_NoException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var exporter = new DebugModeExporter(filePath);

        // Act & Assert
        exporter.Dispose();
        exporter.Dispose();
        exporter.Dispose();
    }

    #endregion

    #region Sequence Number Tests

    [Fact]
    public void CaptureCandle_IncrementsSequenceNumber()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act
        exporter.CaptureCandle(new CandleDataPoint { Time = 1 });
        var seq1 = exporter.CurrentSequence;

        exporter.CaptureCandle(new CandleDataPoint { Time = 2 });
        var seq2 = exporter.CurrentSequence;

        exporter.CaptureCandle(new CandleDataPoint { Time = 3 });
        var seq3 = exporter.CurrentSequence;

        // Assert
        Assert.Equal(1, seq1);
        Assert.Equal(2, seq2);
        Assert.Equal(3, seq3);

        exporter.Cleanup();
    }

    [Fact]
    public async Task SequenceNumbers_ConcurrentCapture_AllUnique()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 5000); // Long interval to avoid flushing
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        const int threadCount = 10;
        const int eventsPerThread = 100;
        var allSequences = new List<long>();
        var lockObj = new object();

        // Act - Multiple threads capturing events
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                for (int i = 0; i < eventsPerThread; i++)
                {
                    var candle = new CandleDataPoint { Time = i };
                    exporter.CaptureCandle(candle);

                    lock (lockObj)
                    {
                        allSequences.Add(candle.SequenceNumber!.Value);
                    }
                }
            })
        ).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(threadCount * eventsPerThread, allSequences.Count);
        Assert.Equal(allSequences.Count, allSequences.Distinct().Count()); // All unique

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureIndicator_SetsSequenceNumber()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        var indicator = new IndicatorDataPoint { Time = 1, Value = 50.5 };

        // Act
        exporter.CaptureIndicator("SMA_20", indicator);

        // Assert
        Assert.NotNull(indicator.SequenceNumber);
        Assert.Equal(1, indicator.SequenceNumber);

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureTrade_SetsSequenceNumber()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        var trade = new TradeDataPoint { Time = 1, Price = 100, Volume = 1, Side = "buy" };

        // Act
        exporter.CaptureTrade(trade);

        // Assert
        Assert.NotNull(trade.SequenceNumber);
        Assert.Equal(1, trade.SequenceNumber);

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureState_SetsSequenceNumber()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        var state = new StateDataPoint { Time = 1, Position = 1.5, PnL = 50.0 };

        // Act
        exporter.CaptureState(state);

        // Assert
        Assert.NotNull(state.SequenceNumber);
        Assert.Equal(1, state.SequenceNumber);

        exporter.Cleanup();
    }

    #endregion

    #region Buffer Integration Tests

    [Fact]
    public async Task CaptureEvents_FlushesToFile()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100); // Fast flush for testing
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act
        exporter.CaptureCandle(new CandleDataPoint { Time = 1, Open = 100, High = 105, Low = 99, Close = 103, Volume = 1000 });
        exporter.CaptureTrade(new TradeDataPoint { Time = 1, Price = 103, Volume = 1, Side = "buy", PnL = 0 });

        // Wait for buffer to flush
        await Task.Delay(200);

        exporter.Cleanup();

        // Assert
        Assert.True(File.Exists(filePath));
        var lines = File.ReadAllLines(filePath);
        Assert.True(lines.Length >= 2); // At least 2 events

        // Verify JSON structure
        var candle = JsonDocument.Parse(lines[0]);
        Assert.Equal("candle", candle.RootElement.GetProperty("type").GetString());

        var trade = JsonDocument.Parse(lines[1]);
        Assert.Equal("trade", trade.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void EventCount_IncreasesWithCaptures()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 50);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act
        for (int i = 0; i < 10; i++)
        {
            exporter.CaptureCandle(new CandleDataPoint { Time = i });
        }

        // Wait for flush
        Thread.Sleep(100);

        // Assert
        Assert.True(exporter.EventCount >= 10);

        exporter.Cleanup();
    }

    #endregion

    #region Capture Method Validation Tests

    [Fact]
    public void CaptureCandle_WithNull_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.CaptureCandle(null!));

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureIndicator_WithNullName_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            exporter.CaptureIndicator(null!, new IndicatorDataPoint()));

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureIndicator_WithNullData_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            exporter.CaptureIndicator("SMA_20", null!));

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureTrade_WithNull_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.CaptureTrade(null!));

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureState_WithNull_ThrowsException()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => exporter.CaptureState(null!));

        exporter.Cleanup();
    }

    [Fact]
    public void CaptureEvents_BeforeInitialize_DoesNothing()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);

        // Act - Capture without initializing (should not throw, just do nothing)
        exporter.CaptureCandle(new CandleDataPoint { Time = 1 });
        exporter.CaptureTrade(new TradeDataPoint { Time = 1 });

        // Assert
        Assert.False(exporter.IsInitialized);
        Assert.Equal(0, exporter.EventCount);
    }

    [Fact]
    public void CaptureEvents_AfterDisposal_DoesNothing()
    {
        // Arrange
        var filePath = GetTestFilePath();
        var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);
        exporter.Dispose();

        // Act - Capture after disposal (should not throw, just do nothing)
        exporter.CaptureCandle(new CandleDataPoint { Time = 1 });

        // Assert
        Assert.Equal(0, exporter.CurrentSequence); // Should not increment
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NullOutputPath_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DebugModeExporter(null!));
    }

    [Fact]
    public void Constructor_EmptyOutputPath_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DebugModeExporter(""));
    }

    [Fact]
    public void Constructor_NegativeFlushInterval_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new DebugModeExporter(GetTestFilePath(), flushIntervalMs: -1));
    }

    [Fact]
    public void Constructor_ZeroFlushInterval_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new DebugModeExporter(GetTestFilePath(), flushIntervalMs: 0));
    }

    [Fact(Skip = "MaxFileSizeMB parameter removed for simplicity")]
    public void Constructor_NegativeMaxFileSize_ThrowsException()
    {
        // Test disabled - maxFileSizeMB parameter removed
    }

    [Fact(Skip = "MaxFileSizeMB parameter removed for simplicity")]
    public void Constructor_ZeroMaxFileSize_ThrowsException()
    {
        // Test disabled - maxFileSizeMB parameter removed
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Act
        using var exporter = new DebugModeExporter(GetTestFilePath(), flushIntervalMs: 500);

        // Assert
        Assert.NotNull(exporter);
        Assert.False(exporter.IsInitialized);
        Assert.Equal(0, exporter.EventCount);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void OutputPath_ReturnsConstructorValue()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);

        // Assert
        Assert.Equal(filePath, exporter.OutputPath);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);

        // Assert
        Assert.False(exporter.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        // Act
        exporter.Initialize(strategy);

        // Assert
        Assert.True(exporter.IsInitialized);

        exporter.Cleanup();
    }

    [Fact]
    public void IsInitialized_AfterCleanup_ReturnsFalse()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();

        exporter.Initialize(strategy);
        Assert.True(exporter.IsInitialized);

        // Act
        exporter.Cleanup();

        // Assert
        Assert.False(exporter.IsInitialized);
    }

    #endregion

    #region Candle Interval Detection Tests

    [Fact]
    public void CandleInterval_BeforeFirstCandle_ReturnsNull()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        // Assert
        Assert.Null(exporter.CandleInterval);

        exporter.Cleanup();
    }

    [Fact]
    public void CandleInterval_AfterOneCandle_ReturnsNull()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        // Act
        var candle = CreateCandleMessage(DateTimeOffset.UtcNow);
        exporter.CaptureCandle(candle, new Messages.SecurityId { SecurityCode = "TEST" });

        // Assert
        Assert.Null(exporter.CandleInterval);

        exporter.Cleanup();
    }

    [Fact]
    public void CandleInterval_AfterTwoCandles_DetectsInterval()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        var time1 = DateTimeOffset.UtcNow;
        var time2 = time1.AddHours(1);

        // Act
        exporter.CaptureCandle(CreateCandleMessage(time1), new Messages.SecurityId { SecurityCode = "TEST" });
        exporter.CaptureCandle(CreateCandleMessage(time2), new Messages.SecurityId { SecurityCode = "TEST" });

        // Assert
        Assert.NotNull(exporter.CandleInterval);
        Assert.Equal(TimeSpan.FromHours(1), exporter.CandleInterval.Value);

        exporter.Cleanup();
    }

    [Fact]
    public void CandleInterval_WithExplicitInterval_UsesConfigured()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        var configuredInterval = TimeSpan.FromMinutes(5);

        // Act
        exporter.Initialize(strategy, configuredInterval);

        // Assert
        Assert.Equal(configuredInterval, exporter.CandleInterval);

        exporter.Cleanup();
    }

    [Fact]
    public void CandleInterval_ExplicitMatchesDetected_NoWarning()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        var interval = TimeSpan.FromHours(1);

        exporter.Initialize(strategy, interval);

        var time1 = DateTimeOffset.UtcNow;
        var time2 = time1.Add(interval);

        // Act - Capture matching interval
        exporter.CaptureCandle(CreateCandleMessage(time1), new Messages.SecurityId { SecurityCode = "TEST" });
        exporter.CaptureCandle(CreateCandleMessage(time2), new Messages.SecurityId { SecurityCode = "TEST" });

        // Assert - Should not change from configured value
        Assert.Equal(interval, exporter.CandleInterval);

        exporter.Cleanup();
    }

    [Fact]
    public void CandleInterval_AfterCleanup_ResetsToNull()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        var time1 = DateTimeOffset.UtcNow;
        var time2 = time1.AddMinutes(5);

        exporter.CaptureCandle(CreateCandleMessage(time1), new Messages.SecurityId { SecurityCode = "TEST" });
        exporter.CaptureCandle(CreateCandleMessage(time2), new Messages.SecurityId { SecurityCode = "TEST" });

        Assert.NotNull(exporter.CandleInterval);

        // Act
        exporter.Cleanup();

        // Assert
        Assert.Null(exporter.CandleInterval);
    }

    [Fact]
    public void CandleInterval_MultipleTimeframes_DetectsFirst()
    {
        // Arrange
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        var baseTime = DateTimeOffset.UtcNow;

        // Act - Send 1-hour candles
        exporter.CaptureCandle(CreateCandleMessage(baseTime), new Messages.SecurityId { SecurityCode = "TEST" });
        exporter.CaptureCandle(CreateCandleMessage(baseTime.AddHours(1)), new Messages.SecurityId { SecurityCode = "TEST" });

        var detectedInterval = exporter.CandleInterval;

        // Then send different interval (should keep first detected)
        exporter.CaptureCandle(CreateCandleMessage(baseTime.AddHours(2)), new Messages.SecurityId { SecurityCode = "TEST" });

        // Assert
        Assert.Equal(TimeSpan.FromHours(1), detectedInterval);

        exporter.Cleanup();
    }

    // Helper method to create candle messages
    private Messages.ICandleMessage CreateCandleMessage(DateTimeOffset time)
    {
        return new Messages.TimeFrameCandleMessage
        {
            OpenTime = time,
            CloseTime = time.AddHours(1),
            OpenPrice = 100,
            HighPrice = 105,
            LowPrice = 99,
            ClosePrice = 103,
            TotalVolume = 1000,
            State = Messages.CandleStates.Finished
        };
    }

    #endregion

    #region Shift-Aware Indicator Export Tests

    [Fact]
    public void GetAdjustedIndicatorTimestamp_WithShiftedValue_ReturnsAdjustedTime()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var shift = 5;
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(
            indicator, 8300m, shift, currentTime, true);

        var adjustedTime = exporter.GetAdjustedIndicatorTimestamp(indicatorValue, currentTime);

        var expectedTime = new DateTimeOffset(2025, 1, 1, 16, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedTime, adjustedTime);

        exporter.Cleanup();
    }

    [Fact]
    public void GetAdjustedIndicatorTimestamp_WithoutInterval_ReturnsFallback()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy);

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(
            indicator, 8300m, 5, currentTime, true);

        var adjustedTime = exporter.GetAdjustedIndicatorTimestamp(indicatorValue, currentTime);

        Assert.Equal(currentTime, adjustedTime);

        exporter.Cleanup();
    }

    [Fact]
    public void GetAdjustedIndicatorTimestamp_WithNonShiftedValue_ReturnsOriginalTime()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var indicator = new TestIndicator();
        var indicatorValue = new TestIndicatorValue(indicator, 8300m, currentTime);

        var adjustedTime = exporter.GetAdjustedIndicatorTimestamp(indicatorValue, currentTime);

        Assert.Equal(currentTime, adjustedTime);

        exporter.Cleanup();
    }

    [Fact]
    public void GetAdjustedIndicatorTimestamp_WithZeroShift_ReturnsOriginalTime()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(
            indicator, 8300m, 0, currentTime, true);

        var adjustedTime = exporter.GetAdjustedIndicatorTimestamp(indicatorValue, currentTime);

        Assert.Equal(currentTime, adjustedTime);

        exporter.Cleanup();
    }

    [Fact]
    public void CreateIndicatorDataPoint_WithShiftedValue_CreatesCorrectDataPoint()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var shift = 5;
        var value = 8300m;
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(
            indicator, value, shift, currentTime, true);
        indicatorValue.IsFormed = true;

        var dataPoint = exporter.CreateIndicatorDataPoint(indicatorValue);

        Assert.NotNull(dataPoint);
        var expectedTime = new DateTimeOffset(2025, 1, 1, 16, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedTime.ToUnixTimeMilliseconds(), dataPoint.Time);
        Assert.Equal((double)value, dataPoint.Value);
        Assert.NotNull(dataPoint.SequenceNumber);

        exporter.Cleanup();
    }

    [Fact]
    public void CreateIndicatorDataPoint_WithEmptyValue_ReturnsNull()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(indicator, currentTime);

        var dataPoint = exporter.CreateIndicatorDataPoint(indicatorValue);

        Assert.Null(dataPoint);

        exporter.Cleanup();
    }

    [Fact]
    public void CreateIndicatorDataPoint_WithZeroValue_ReturnsNull()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var indicator = new TestIndicator();
        var indicatorValue = new StockSharp.Algo.Indicators.ZigZagIndicatorValue(
            indicator, 0m, 0, currentTime, true);

        var dataPoint = exporter.CreateIndicatorDataPoint(indicatorValue);

        Assert.Null(dataPoint);

        exporter.Cleanup();
    }

    [Fact]
    public void CreateIndicatorDataPoint_WithNormalValue_CreatesDataPoint()
    {
        var filePath = GetTestFilePath();
        using var exporter = new DebugModeExporter(filePath);
        var strategy = new TestStrategy();
        exporter.Initialize(strategy, TimeSpan.FromHours(1));

        var currentTime = new DateTimeOffset(2025, 1, 1, 21, 0, 0, TimeSpan.Zero);
        var value = 8300m;
        var indicator = new TestIndicator();
        var indicatorValue = new TestIndicatorValue(indicator, value, currentTime);
        indicatorValue.IsFormed = true;

        var dataPoint = exporter.CreateIndicatorDataPoint(indicatorValue);

        Assert.NotNull(dataPoint);
        Assert.Equal(currentTime.ToUnixTimeMilliseconds(), dataPoint.Time);
        Assert.Equal((double)value, dataPoint.Value);

        exporter.Cleanup();
    }

    #endregion
}
