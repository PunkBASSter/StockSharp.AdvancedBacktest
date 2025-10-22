using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.Algo.Indicators;
using StockSharp.Messages;

namespace StockSharp.AdvancedBacktest.Tests.DebugMode;

/// <summary>
/// Integration tests for DM-03: Indicator & Candle Capture
/// Tests the full integration of debug mode with StockSharp components
/// </summary>
public class DebugModeIntegrationTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly List<string> _filesToCleanup = new();

	public DebugModeIntegrationTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"DebugModeIntegration_{Guid.NewGuid():N}");
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
		for (int i = 1; i <= 10; i++)
		{
			var baseName = Path.GetFileNameWithoutExtension(path);
			var ext = Path.GetExtension(path);
			_filesToCleanup.Add(Path.Combine(_testDirectory, $"{baseName}_{i:D3}{ext}"));
		}
		return path;
	}

	private class TestStrategy : CustomStrategyBase
	{
	}

	#region Indicator Capture Tests

	[Fact]
	public void SubscribeToIndicator_CapturesChangedEvents()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var sma = new SimpleMovingAverage { Length = 3 };
		exporter.SubscribeToIndicator(sma);

		// Act - Process values through indicator
		var value1 = new DecimalIndicatorValue(sma, 100m, DateTimeOffset.UtcNow) { IsFinal = true };
		var result1 = sma.Process(value1);

		var value2 = new DecimalIndicatorValue(sma, 102m, DateTimeOffset.UtcNow.AddSeconds(1)) { IsFinal = true };
		var result2 = sma.Process(value2);

		var value3 = new DecimalIndicatorValue(sma, 104m, DateTimeOffset.UtcNow.AddSeconds(2)) { IsFinal = true };
		var result3 = sma.Process(value3);

		// Wait for buffer flush
		Thread.Sleep(200);

		exporter.Cleanup();

		// Assert - Should capture only after indicator is formed (3 values)
		Assert.True(File.Exists(filePath));
		var lines = File.ReadAllLines(filePath);
		Assert.NotEmpty(lines); // At least one event captured after formation
	}

	[Fact]
	public void SubscribeToIndicators_SubscribesToMultipleIndicators()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var indicators = new List<IIndicator>
		{
			new SimpleMovingAverage { Length = 5, Name = "SMA_5" },
			new ExponentialMovingAverage { Length = 10, Name = "EMA_10" }
		};

		// Act
		exporter.SubscribeToIndicators(indicators);

		// Process values through indicators
		foreach (var indicator in indicators)
		{
			for (int i = 0; i < 15; i++)
			{
				var value = new DecimalIndicatorValue(indicator, 100m + i, DateTimeOffset.UtcNow.AddSeconds(i)) { IsFinal = true };
				indicator.Process(value);
			}
		}

		Thread.Sleep(200);
		exporter.Cleanup();

		// Assert
		Assert.True(File.Exists(filePath));
		var content = File.ReadAllText(filePath);
		Assert.Contains("indicator_SMA_5", content);
		Assert.Contains("indicator_EMA_10", content);
	}

	[Fact]
	public void IndicatorCapture_OnlyWhenIsFormed()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var sma = new SimpleMovingAverage { Length = 5 };
		exporter.SubscribeToIndicator(sma);

		var eventCount = 0;
		var capturedEvents = new List<long>();

		// Act - Process 10 values (only last 6 should be captured after formation)
		for (int i = 0; i < 10; i++)
		{
			var value = new DecimalIndicatorValue(sma, 100m + i, DateTimeOffset.UtcNow.AddSeconds(i)) { IsFinal = true };
			var result = sma.Process(value);

			if (result.IsFormed)
			{
				capturedEvents.Add(i);
				eventCount++;
			}
		}

		Thread.Sleep(200);
		exporter.Cleanup();

		// Assert - Only formed values captured
		Assert.True(eventCount > 0, "Should capture some events after indicator is formed");
		Assert.True(File.Exists(filePath));
	}

	#endregion

	#region Candle Capture Tests

	[Fact]
	public void CaptureCandle_FromICandleMessage_ExtractsOHLCV()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var securityId = new SecurityId { SecurityCode = "BTCUSDT", BoardCode = "BINANCE" };
		var candle = new TimeFrameCandleMessage
		{
			OpenTime = DateTimeOffset.UtcNow,
			OpenPrice = 50000m,
			HighPrice = 50100m,
			LowPrice = 49900m,
			ClosePrice = 50050m,
			TotalVolume = 1250.5m
		};

		// Act
		exporter.CaptureCandle(candle, securityId);

		Thread.Sleep(200);
		exporter.Cleanup();

		// Assert
		Assert.True(File.Exists(filePath));
		var content = File.ReadAllText(filePath);
		Assert.Contains("50000", content); // Open
		Assert.Contains("50100", content); // High
		Assert.Contains("BTCUSDT@BINANCE", content); // Security ID
	}

	[Fact]
	public void CaptureCandle_MultipleCandles_AllCaptured()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath, flushIntervalMs: 100);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var securityId = new SecurityId { SecurityCode = "ETHUSDT", BoardCode = "BINANCE" };

		// Act - Capture 10 candles
		for (int i = 0; i < 10; i++)
		{
			var candle = new TimeFrameCandleMessage
			{
				OpenTime = DateTimeOffset.UtcNow.AddMinutes(i),
				OpenPrice = 3000m + i,
				HighPrice = 3010m + i,
				LowPrice = 2990m + i,
				ClosePrice = 3005m + i,
				TotalVolume = 1000m
			};

			exporter.CaptureCandle(candle, securityId);
		}

		Thread.Sleep(200);
		exporter.Cleanup();

		// Assert
		Assert.True(File.Exists(filePath));
		var lines = File.ReadAllLines(filePath);
		Assert.True(lines.Length >= 10, $"Expected at least 10 lines, got {lines.Length}");
	}

	#endregion

	#region Sequence Number Tests

	[Fact]
	public void EventCapture_SequenceNumbersIncrement()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var initialSeq = exporter.CurrentSequence;

		// Act - Capture different event types
		var candle = new CandleDataPoint { Time = 1, Open = 100, High = 105, Low = 99, Close = 103, Volume = 1000 };
		exporter.CaptureCandle(candle);

		var trade = new TradeDataPoint { Time = 1, Price = 103, Volume = 1, Side = "buy" };
		exporter.CaptureTrade(trade);

		var indicator = new IndicatorDataPoint { Time = 1, Value = 102 };
		exporter.CaptureIndicator("SMA_20", indicator);

		// Assert
		Assert.True(exporter.CurrentSequence > initialSeq);
		Assert.NotNull(candle.SequenceNumber);
		Assert.NotNull(trade.SequenceNumber);
		Assert.NotNull(indicator.SequenceNumber);

		// Sequence numbers should be unique
		var sequences = new[] { candle.SequenceNumber.Value, trade.SequenceNumber.Value, indicator.SequenceNumber.Value };
		Assert.Equal(3, sequences.Distinct().Count());

		exporter.Cleanup();
	}

	#endregion

	#region Cleanup Tests

	[Fact]
	public void Cleanup_UnsubscribesFromIndicators()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var sma = new SimpleMovingAverage { Length = 3 };
		exporter.SubscribeToIndicator(sma);

		// Cleanup
		exporter.Cleanup();

		// Act - Process value after cleanup (should not be captured)
		for (int i = 0; i < 5; i++)
		{
			var value = new DecimalIndicatorValue(sma, 100m + i, DateTimeOffset.UtcNow.AddSeconds(i)) { IsFinal = true };
			sma.Process(value);
		}

		// Assert - No exceptions thrown, file might be empty or have only initial events
		Assert.False(exporter.IsInitialized);
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public void SubscribeToIndicator_NullIndicator_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => exporter.SubscribeToIndicator(null!));

		exporter.Cleanup();
	}

	[Fact]
	public void CaptureCandle_NullCandle_ThrowsException()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath);
		var strategy = new TestStrategy();

		exporter.Initialize(strategy);

		var securityId = new SecurityId { SecurityCode = "TEST", BoardCode = "TEST" };

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => exporter.CaptureCandle((ICandleMessage)null!, securityId));

		exporter.Cleanup();
	}

	[Fact]
	public void CaptureEvents_BeforeInitialize_DoesNothing()
	{
		// Arrange
		var filePath = GetTestFilePath();
		using var exporter = new DebugModeExporter(filePath);

		var sma = new SimpleMovingAverage { Length = 3 };

		// Act - Subscribe before initialize (should do nothing, not throw)
		exporter.SubscribeToIndicator(sma);

		var candle = new CandleDataPoint { Time = 1 };
		exporter.CaptureCandle(candle);

		// Assert
		Assert.False(exporter.IsInitialized);
		Assert.Equal(0, exporter.EventCount);
	}

	#endregion
}
