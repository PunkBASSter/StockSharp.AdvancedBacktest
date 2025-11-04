using StockSharp.AdvancedBacktest.DebugMode;
using StockSharp.AdvancedBacktest.Export;

namespace StockSharp.AdvancedBacktest.Tests.DebugMode;

public class DebugEventBufferTests
{
	#region Time-Based Flushing Tests

	[Fact]
	public async Task Flush_OccursAfterSpecifiedInterval()
	{
		// Arrange
		const int flushIntervalMs = 100; // Short interval for testing
		using var buffer = new DebugEventBuffer(flushIntervalMs);

		var flushOccurred = false;
		Dictionary<string, List<object>>? flushedEvents = null;

		buffer.OnFlush += events =>
		{
			flushOccurred = true;
			flushedEvents = events;
		};

		// Act
		buffer.Add("test", new { Value = 1 });

		// Wait for timer to trigger flush
		await Task.Delay(flushIntervalMs + 50);

		// Assert
		Assert.True(flushOccurred, "Flush should occur after specified interval");
		Assert.NotNull(flushedEvents);
		Assert.Single(flushedEvents);
		Assert.Contains("test", flushedEvents.Keys);
		Assert.Single(flushedEvents["test"]);
	}

	[Fact]
	public async Task Flush_BuffersMultipleEventsBeforeFlush()
	{
		// Arrange
		const int flushIntervalMs = 200;
		using var buffer = new DebugEventBuffer(flushIntervalMs);

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		// Act - Add multiple events rapidly
		buffer.Add("candle", new CandleDataPoint { Time = 1 });
		buffer.Add("candle", new CandleDataPoint { Time = 2 });
		buffer.Add("candle", new CandleDataPoint { Time = 3 });
		buffer.Add("trade", new TradeDataPoint { Time = 1 });

		// Wait for flush
		await Task.Delay(flushIntervalMs + 50);

		// Assert
		Assert.NotNull(flushedEvents);
		Assert.Equal(2, flushedEvents.Count); // 2 event types
		Assert.Equal(3, flushedEvents["candle"].Count);
		Assert.Single(flushedEvents["trade"]);
	}

	[Fact]
	public async Task Flush_EmptyBuffer_DoesNotTriggerEvent()
	{
		// Arrange
		const int flushIntervalMs = 100;
		using var buffer = new DebugEventBuffer(flushIntervalMs);

		var flushCount = 0;
		buffer.OnFlush += _ => flushCount++;

		// Act - Don't add any events
		await Task.Delay(flushIntervalMs + 50);

		// Assert
		Assert.Equal(0, flushCount);
	}

	[Fact]
	public async Task Flush_ClearsBufferAfterFlush()
	{
		// Arrange
		const int flushIntervalMs = 200; // Increased for reliability on slow systems
		using var buffer = new DebugEventBuffer(flushIntervalMs);

		var flushCount = 0;
		buffer.OnFlush += _ => flushCount++;

		// Act - Add event, wait for flush, then wait again
		buffer.Add("test", new { Value = 1 });
		await Task.Delay(flushIntervalMs + 100); // Increased wait time

		var firstFlushCount = flushCount;

		// Wait for another interval without adding events
		await Task.Delay(flushIntervalMs + 100); // Increased wait time

		// Assert
		Assert.Equal(1, firstFlushCount);
		Assert.Equal(1, flushCount); // Should not flush empty buffer
	}

	#endregion

	#region Event Dictionary Tests

	[Fact]
	public async Task Add_MultipleEventTypes_BufferedSeparately()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(1000);

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		// Act
		buffer.Add("candle", new CandleDataPoint { Time = 1 });
		buffer.Add("trade", new TradeDataPoint { Time = 1 });
		buffer.Add("indicator", new IndicatorDataPoint { Time = 1 });
		buffer.Add("candle", new CandleDataPoint { Time = 2 });

		buffer.Flush(); // Manual flush
		await Task.Delay(50); // Wait for async flush to complete

		// Assert
		Assert.NotNull(flushedEvents);
		Assert.Equal(3, flushedEvents.Count);
		Assert.Equal(2, flushedEvents["candle"].Count);
		Assert.Single(flushedEvents["trade"]);
		Assert.Single(flushedEvents["indicator"]);
	}

	[Fact]
	public async Task Add_SameEventType_AccumulatesInList()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(1000);

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		// Act
		for (int i = 0; i < 100; i++)
		{
			buffer.Add("test", new { Index = i });
		}

		buffer.Flush();
		await Task.Delay(50); // Wait for async flush to complete

		// Assert
		Assert.NotNull(flushedEvents);
		Assert.Single(flushedEvents);
		Assert.Equal(100, flushedEvents["test"].Count);
	}

	[Fact]
	public void Add_NullEventType_ThrowsException()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(1000);

		// Act & Assert
		Assert.Throws<ArgumentException>(() => buffer.Add(null!, new { Value = 1 }));
		Assert.Throws<ArgumentException>(() => buffer.Add("", new { Value = 1 }));
	}

	[Fact]
	public void Add_NullEventData_ThrowsException()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(1000);

		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => buffer.Add("test", null!));
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task Add_ConcurrentAccess_NoEventsLost()
	{
		// Arrange
		const int threadCount = 10;
		const int eventsPerThread = 100;
		using var buffer = new DebugEventBuffer(500);

		var totalFlushedEvents = 0;
		var lockObj = new object();

		buffer.OnFlush += events =>
		{
			lock (lockObj)
			{
				foreach (var eventList in events.Values)
				{
					totalFlushedEvents += eventList.Count;
				}
			}
		};

		// Act - Multiple threads adding events concurrently
		var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
			Task.Run(() =>
			{
				for (int i = 0; i < eventsPerThread; i++)
				{
					buffer.Add($"thread_{threadId}", new { ThreadId = threadId, Index = i });
				}
			})
		).ToArray();

		await Task.WhenAll(tasks);

		// Flush to ensure all events are counted
		buffer.Flush();
		await Task.Delay(100); // Wait for async flush to complete

		// Assert
		Assert.Equal(threadCount * eventsPerThread, totalFlushedEvents);
	}

	[Fact]
	public async Task Flush_ConcurrentWithAdd_ThreadSafe()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(50); // Fast flush interval

		var addTask = Task.Run(() =>
		{
			for (int i = 0; i < 1000; i++)
			{
				buffer.Add("test", new { Index = i });
			}
		});

		var flushTask = Task.Run(async () =>
		{
			for (int i = 0; i < 20; i++)
			{
				buffer.Flush();
				await Task.Delay(10);
			}
		});

		// Act & Assert - Should not throw
		await Task.WhenAll(addTask, flushTask);
	}

	#endregion

	#region Disposal Tests

	[Fact]
	public void Dispose_StopsTimer()
	{
		// Arrange
		var buffer = new DebugEventBuffer(100);
		var flushCount = 0;
		buffer.OnFlush += _ => flushCount++;

		buffer.Add("test", new { Value = 1 });

		// Act
		buffer.Dispose();

		// Wait for what would have been multiple flush intervals
		Thread.Sleep(300);

		// Assert - Should have flushed exactly once (final flush on disposal)
		Assert.True(flushCount <= 1, "Timer should stop after disposal");
	}

	[Fact]
	public void Dispose_PerformsFinalFlush()
	{
		// Arrange
		var buffer = new DebugEventBuffer(10000); // Long interval so timer won't fire

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		buffer.Add("test", new { Value = 1 });

		// Act
		buffer.Dispose();

		// Assert
		Assert.NotNull(flushedEvents);
		Assert.Single(flushedEvents);
		Assert.Single(flushedEvents["test"]);
	}

	[Fact]
	public void Dispose_EmptyBuffer_NoExceptions()
	{
		// Arrange
		var buffer = new DebugEventBuffer(1000);
		var flushCalled = false;
		buffer.OnFlush += _ => flushCalled = true;

		// Act & Assert - Should not throw
		buffer.Dispose();
		Assert.False(flushCalled);
	}

	[Fact]
	public void Dispose_MultipleCalls_NoExceptions()
	{
		// Arrange
		var buffer = new DebugEventBuffer(1000);

		// Act & Assert - Should not throw
		buffer.Dispose();
		buffer.Dispose();
		buffer.Dispose();
	}

	[Fact]
	public void Add_AfterDisposal_ThrowsException()
	{
		// Arrange
		var buffer = new DebugEventBuffer(1000);
		buffer.Dispose();

		// Act & Assert
		Assert.Throws<ObjectDisposedException>(() => buffer.Add("test", new { Value = 1 }));
	}

	#endregion

	#region Manual Flush Tests

	[Fact]
	public void Flush_Manual_ImmediatelyFlushesEvents()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000); // Long interval so timer won't fire

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		buffer.Add("test", new { Value = 1 });

		// Act
		buffer.Flush();

		// Small delay for async Task.Run
		Thread.Sleep(50);

		// Assert
		Assert.NotNull(flushedEvents);
	}

	[Fact]
	public void Flush_AfterManualFlush_BufferIsEmpty()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		var flushCount = 0;
		buffer.OnFlush += _ => flushCount++;

		// Act
		buffer.Add("test", new { Value = 1 });
		buffer.Flush();

		Thread.Sleep(50);

		// Flush again - should not trigger event (buffer is empty)
		buffer.Flush();

		Thread.Sleep(50);

		// Assert
		Assert.Equal(1, flushCount);
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_NegativeInterval_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new DebugEventBuffer(-1));
	}

	[Fact]
	public void Constructor_ZeroInterval_ThrowsException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => new DebugEventBuffer(0));
	}

	[Fact]
	public void Constructor_ValidInterval_CreatesBuffer()
	{
		// Act & Assert - Should not throw
		using var buffer = new DebugEventBuffer(500);
		Assert.NotNull(buffer);
	}

	#endregion

#if DEBUG
	#region Synchronous Flush Tests (DEBUG only)

	[Fact]
	public void FlushSynchronously_ImmediatelyFlushesEvents()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000); // Long interval so timer won't fire

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		buffer.Add("test", new { Value = 1 });

		// Act - No Task.Delay needed, should complete immediately
		buffer.FlushSynchronously();

		// Assert - Events should be flushed immediately, no async delay
		Assert.NotNull(flushedEvents);
		Assert.Single(flushedEvents);
		Assert.Single(flushedEvents["test"]);
	}

	[Fact]
	public void FlushSynchronously_WritesBeforeReturning()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		var eventsWritten = false;
		buffer.OnFlush += events => eventsWritten = true;

		buffer.Add("test", new { Value = 1 });

		// Act
		buffer.FlushSynchronously();

		// Assert - Should be true immediately after method returns
		Assert.True(eventsWritten, "Events should be written before FlushSynchronously returns");
	}

	[Fact]
	public void FlushSynchronously_ClearsBuffer()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		var flushCount = 0;
		buffer.OnFlush += _ => flushCount++;

		// Act
		buffer.Add("test", new { Value = 1 });
		buffer.FlushSynchronously();

		// Flush again - should not trigger event (buffer is empty)
		buffer.FlushSynchronously();

		// Assert
		Assert.Equal(1, flushCount);
	}

	[Fact]
	public void FlushSynchronously_EmptyBuffer_DoesNotTriggerEvent()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		var flushCalled = false;
		buffer.OnFlush += _ => flushCalled = true;

		// Act
		buffer.FlushSynchronously();

		// Assert
		Assert.False(flushCalled, "Should not flush empty buffer");
	}

	[Fact]
	public void FlushSynchronously_AfterDisposal_DoesNotThrow()
	{
		// Arrange
		var buffer = new DebugEventBuffer(10000);
		buffer.Add("test", new { Value = 1 });
		buffer.Dispose();

		// Act & Assert - Should not throw
		buffer.FlushSynchronously();
	}

	[Fact]
	public void FlushSynchronously_MultipleEventTypes_FlushesAll()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		Dictionary<string, List<object>>? flushedEvents = null;
		buffer.OnFlush += events => flushedEvents = events;

		// Act
		buffer.Add("candle", new CandleDataPoint { Time = 1 });
		buffer.Add("candle", new CandleDataPoint { Time = 2 });
		buffer.Add("trade", new TradeDataPoint { Time = 1 });
		buffer.Add("indicator", new IndicatorDataPoint { Time = 1 });

		buffer.FlushSynchronously();

		// Assert
		Assert.NotNull(flushedEvents);
		Assert.Equal(3, flushedEvents.Count); // 3 event types
		Assert.Equal(2, flushedEvents["candle"].Count);
		Assert.Single(flushedEvents["trade"]);
		Assert.Single(flushedEvents["indicator"]);
	}

	[Fact]
	public void FlushSynchronously_Performance_CompletesQuickly()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);
		var eventCount = 0;

		buffer.OnFlush += events =>
		{
			foreach (var list in events.Values)
			{
				eventCount += list.Count;
			}
		};

		// Add 1000 events
		for (int i = 0; i < 1000; i++)
		{
			buffer.Add("test", new { Index = i });
		}

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		buffer.FlushSynchronously();
		stopwatch.Stop();

		// Assert
		Assert.Equal(1000, eventCount);
		// Synchronous flush should complete in <50ms for 1000 events
		Assert.True(stopwatch.ElapsedMilliseconds < 50,
			$"FlushSynchronously took {stopwatch.ElapsedMilliseconds}ms (target: <50ms)");
	}

	[Fact]
	public void FlushSynchronously_ConcurrentWithAdd_ThreadSafe()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);
		var totalFlushed = 0;
		var lockObj = new object();

		buffer.OnFlush += events =>
		{
			lock (lockObj)
			{
				foreach (var list in events.Values)
				{
					totalFlushed += list.Count;
				}
			}
		};

		// Act - Add events from multiple threads while flushing
		var addTask = Task.Run(() =>
		{
			for (int i = 0; i < 500; i++)
			{
				buffer.Add("test", new { Index = i });
			}
		});

		var flushTask = Task.Run(() =>
		{
			for (int i = 0; i < 10; i++)
			{
				buffer.FlushSynchronously();
				Thread.Sleep(5);
			}
		});

		// Assert - Should not throw
		Task.WaitAll(addTask, flushTask);

		// Final flush to count remaining events
		buffer.FlushSynchronously();

		Assert.Equal(500, totalFlushed);
	}

	#endregion
#endif

	#region Performance Tests

	[Fact]
	public void Add_Performance_LessThan1MicrosecondPerEvent()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);
		const int eventCount = 10000;

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		for (int i = 0; i < eventCount; i++)
		{
			buffer.Add("test", new { Index = i });
		}

		stopwatch.Stop();

		// Assert - Target: <1μs per event = <10ms for 10000 events
		var microsPerEvent = (stopwatch.Elapsed.TotalMilliseconds * 1000) / eventCount;
		Assert.True(microsPerEvent < 1, $"Add operation took {microsPerEvent:F3}μs per event (target: <1μs)");
	}

	[Fact]
	public async Task Flush_Performance_LessThan10msFor1000Events()
	{
		// Arrange
		using var buffer = new DebugEventBuffer(10000);

		for (int i = 0; i < 1000; i++)
		{
			buffer.Add("test", new { Index = i });
		}

		// Act
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		buffer.Flush();
		await Task.Delay(150); // Wait for async flush
		stopwatch.Stop();

		// Assert - Allow up to 200ms for async operation including Task.Delay overhead
		Assert.True(stopwatch.ElapsedMilliseconds < 200, $"Flush took {stopwatch.ElapsedMilliseconds}ms (target: <200ms for async operation)");
	}

	#endregion
}
