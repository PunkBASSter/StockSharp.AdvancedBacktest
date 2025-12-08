using System.Text.Json;
using System.Text.Json.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using Xunit;

namespace StockSharp.AdvancedBacktest.Tests.AiAgenticDebug.EventLogging.Serialization;

public sealed class EventJsonContextTests
{
	[Fact]
	public void EventJsonContext_ShouldUseCamelCaseNaming()
	{
		var entity = new EventEntity
		{
			EventId = "test-123",
			RunId = "run-456",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}"
		};

		var json = JsonSerializer.Serialize(entity, EventJsonContext.Default.EventEntity);

		Assert.Contains("\"eventId\":", json);
		Assert.Contains("\"runId\":", json);
		Assert.Contains("\"eventType\":", json);
		Assert.DoesNotContain("\"EventId\":", json);
		Assert.DoesNotContain("\"RunId\":", json);
	}

	[Fact]
	public void EventJsonContext_ShouldIgnoreNullValues()
	{
		var entity = new EventEntity
		{
			EventId = "test-123",
			RunId = "run-456",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = "{}",
			ParentEventId = null,
			ValidationErrors = null
		};

		var json = JsonSerializer.Serialize(entity, EventJsonContext.Default.EventEntity);

		Assert.DoesNotContain("\"parentEventId\":", json);
		Assert.DoesNotContain("\"validationErrors\":", json);
	}

	[Fact]
	public void EventJsonContext_ShouldSerializeEventEntity()
	{
		var entity = new EventEntity
		{
			EventId = "test-event",
			RunId = "test-run",
			Timestamp = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"price": 100.50}"""
		};

		var json = JsonSerializer.Serialize(entity, EventJsonContext.Default.EventEntity);
		var deserialized = JsonSerializer.Deserialize(json, EventJsonContext.Default.EventEntity);

		Assert.NotNull(deserialized);
		Assert.Equal(entity.EventId, deserialized.EventId);
		Assert.Equal(entity.RunId, deserialized.RunId);
		Assert.Equal(entity.EventType, deserialized.EventType);
		Assert.Equal(entity.Severity, deserialized.Severity);
	}

	[Fact]
	public void EventJsonContext_ShouldSerializeBacktestRunEntity()
	{
		var run = new BacktestRunEntity
		{
			Id = "run-123",
			StartTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
			EndTime = new DateTime(2025, 1, 15, 16, 0, 0, DateTimeKind.Utc),
			StrategyConfigHash = new string('a', 64)
		};

		var json = JsonSerializer.Serialize(run, EventJsonContext.Default.BacktestRunEntity);
		var deserialized = JsonSerializer.Deserialize(json, EventJsonContext.Default.BacktestRunEntity);

		Assert.NotNull(deserialized);
		Assert.Equal(run.Id, deserialized.Id);
		Assert.Equal(run.StartTime, deserialized.StartTime);
		Assert.Equal(run.EndTime, deserialized.EndTime);
		Assert.Equal(run.StrategyConfigHash, deserialized.StrategyConfigHash);
	}

	[Fact]
	public void EventJsonContext_ShouldSerializeValidationMetadata()
	{
		var errors = new List<ValidationError>
		{
			new("Field1", "Error message", "Error"),
			new("Field2", "Warning message", "Warning")
		};

		var json = JsonSerializer.Serialize(errors, EventJsonContext.Default.ListValidationError);
		var deserialized = JsonSerializer.Deserialize(json, EventJsonContext.Default.ListValidationError);

		Assert.NotNull(deserialized);
		Assert.Equal(2, deserialized.Count);
		Assert.Equal("Field1", deserialized[0].Field);
		Assert.Equal("Error message", deserialized[0].Error);
		Assert.Equal("Error", deserialized[0].Severity);
	}

	[Fact]
	public void EventJsonContext_ShouldSerializeEventQueryResult()
	{
		var result = new EventQueryResult
		{
			Events = new[]
			{
				new EventEntity
				{
					EventId = "event-1",
					RunId = "run-1",
					Timestamp = DateTime.UtcNow,
					EventType = EventType.TradeExecution,
					Severity = EventSeverity.Info,
					Category = EventCategory.Execution,
					Properties = "{}"
				}
			},
			Metadata = new QueryResultMetadata
			{
				TotalCount = 100,
				ReturnedCount = 1,
				PageIndex = 0,
				PageSize = 10,
				HasMore = true,
				QueryTimeMs = 150,
				Truncated = false
			}
		};

		var json = JsonSerializer.Serialize(result, EventJsonContext.Default.EventQueryResult);
		var deserialized = JsonSerializer.Deserialize(json, EventJsonContext.Default.EventQueryResult);

		Assert.NotNull(deserialized);
		Assert.Single(deserialized.Events);
		Assert.Equal(100, deserialized.Metadata.TotalCount);
		Assert.True(deserialized.Metadata.HasMore);
	}

	[Fact]
	public void EventJsonContext_ShouldPreserveDecimalPrecision()
	{
		var entity = new EventEntity
		{
			EventId = "test",
			RunId = "run",
			Timestamp = DateTime.UtcNow,
			EventType = EventType.TradeExecution,
			Severity = EventSeverity.Info,
			Category = EventCategory.Execution,
			Properties = """{"price": 100.123456789, "quantity": 50.987654321}"""
		};

		var json = JsonSerializer.Serialize(entity, EventJsonContext.Default.EventEntity);
		using var doc = JsonDocument.Parse(json);
		var properties = doc.RootElement.GetProperty("properties").GetString();

		Assert.NotNull(properties);
		using var propsDoc = JsonDocument.Parse(properties);
		var price = propsDoc.RootElement.GetProperty("price").GetDecimal();
		var quantity = propsDoc.RootElement.GetProperty("quantity").GetDecimal();

		Assert.Equal(100.123456789m, price);
		Assert.Equal(50.987654321m, quantity);
	}

	[Fact]
	public void EventJsonContext_ShouldSerializeEnums()
	{
		var json = JsonSerializer.Serialize(EventType.TradeExecution, EventJsonContext.Default.EventType);

		var deserializedEventType = JsonSerializer.Deserialize(json, EventJsonContext.Default.EventType);
		Assert.Equal(EventType.TradeExecution, deserializedEventType);
	}

	[Fact]
	public void EventJsonContext_ShouldHaveAllRequiredTypeInfos()
	{
		Assert.NotNull(EventJsonContext.Default.EventEntity);
		Assert.NotNull(EventJsonContext.Default.BacktestRunEntity);
		Assert.NotNull(EventJsonContext.Default.ValidationError);
		Assert.NotNull(EventJsonContext.Default.ValidationMetadata);
		Assert.NotNull(EventJsonContext.Default.ListValidationError);
		Assert.NotNull(EventJsonContext.Default.EventQueryResult);
		Assert.NotNull(EventJsonContext.Default.QueryResultMetadata);
		Assert.NotNull(EventJsonContext.Default.EventType);
		Assert.NotNull(EventJsonContext.Default.EventSeverity);
		Assert.NotNull(EventJsonContext.Default.EventCategory);
	}

	[Fact]
	public void EventJsonContext_Options_ShouldNotBeNull()
	{
		Assert.NotNull(EventJsonContext.Default.Options);
	}

	[Fact]
	public void JsonSerializerOptionsProvider_ShouldUseSourceGeneratedContext()
	{
		var options = JsonSerializerOptionsProvider.Options;

		Assert.NotNull(options);
		Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
		Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
	}
}
