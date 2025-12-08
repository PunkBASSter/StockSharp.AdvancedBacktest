using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Validation;

public sealed class EventValidator
{
	private const int MaxPropertiesSizeBytes = 1024 * 1024; // 1MB

	public ValidationMetadata ValidateEvent(EventEntity entity)
	{
		ArgumentNullException.ThrowIfNull(entity);

		var errors = new List<ValidationError>();

		// Validate Properties JSON
		if (!TryParseProperties(entity.Properties, out var propertiesDoc, out var jsonError))
		{
			errors.Add(new ValidationError("Properties", $"Invalid JSON: {jsonError}", "Error"));
			return new ValidationMetadata { Errors = errors };
		}

		// Validate Properties size
		if (entity.Properties.Length > MaxPropertiesSizeBytes)
		{
			errors.Add(new ValidationError("Properties", $"Properties exceeds maximum size of {MaxPropertiesSizeBytes} bytes", "Error"));
		}

		// Validate by event type
		errors.AddRange(ValidateByEventType(entity.EventType, propertiesDoc!.RootElement));

		propertiesDoc?.Dispose();

		return new ValidationMetadata { Errors = errors };
	}

	private static bool TryParseProperties(string properties, out JsonDocument? document, out string? error)
	{
		document = null;
		error = null;

		try
		{
			document = JsonDocument.Parse(properties);
			return true;
		}
		catch (JsonException ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static IEnumerable<ValidationError> ValidateByEventType(EventType eventType, JsonElement properties)
	{
		return eventType switch
		{
			EventType.TradeExecution => ValidateTradeExecution(properties),
			EventType.OrderRejection => ValidateOrderRejection(properties),
			EventType.IndicatorCalculation => ValidateIndicatorCalculation(properties),
			EventType.PositionUpdate => ValidatePositionUpdate(properties),
			EventType.StateChange => ValidateStateChange(properties),
			EventType.MarketDataEvent => ValidateMarketDataEvent(properties),
			EventType.RiskEvent => ValidateRiskEvent(properties),
			_ => []
		};
	}

	private static IEnumerable<ValidationError> ValidateTradeExecution(JsonElement properties)
	{
		var errors = new List<ValidationError>();

		if (!HasProperty(properties, "OrderId"))
			errors.Add(new ValidationError("Properties.OrderId", "Missing required field", "Error"));

		if (!HasProperty(properties, "Price"))
			errors.Add(new ValidationError("Properties.Price", "Missing required field", "Error"));

		if (!HasProperty(properties, "Quantity"))
			errors.Add(new ValidationError("Properties.Quantity", "Missing recommended field", "Warning"));

		return errors;
	}

	private static IEnumerable<ValidationError> ValidateOrderRejection(JsonElement properties)
	{
		var errors = new List<ValidationError>();

		if (!HasProperty(properties, "OrderId"))
			errors.Add(new ValidationError("Properties.OrderId", "Missing required field", "Error"));

		if (!HasProperty(properties, "RejectionReason"))
			errors.Add(new ValidationError("Properties.RejectionReason", "Missing required field", "Error"));

		return errors;
	}

	private static IEnumerable<ValidationError> ValidateIndicatorCalculation(JsonElement properties)
	{
		var errors = new List<ValidationError>();

		if (!HasProperty(properties, "IndicatorName"))
			errors.Add(new ValidationError("Properties.IndicatorName", "Missing required field", "Error"));

		if (!HasProperty(properties, "Value"))
			errors.Add(new ValidationError("Properties.Value", "Missing required field", "Error"));

		return errors;
	}

	private static IEnumerable<ValidationError> ValidatePositionUpdate(JsonElement properties)
	{
		var errors = new List<ValidationError>();

		if (!HasProperty(properties, "SecuritySymbol"))
			errors.Add(new ValidationError("Properties.SecuritySymbol", "Missing required field", "Error"));

		return errors;
	}

	private static IEnumerable<ValidationError> ValidateStateChange(JsonElement properties)
	{
		// StateChange has flexible schema - no required fields
		return [];
	}

	private static IEnumerable<ValidationError> ValidateMarketDataEvent(JsonElement properties)
	{
		// MarketDataEvent has flexible schema - no required fields
		return [];
	}

	private static IEnumerable<ValidationError> ValidateRiskEvent(JsonElement properties)
	{
		var errors = new List<ValidationError>();

		if (!HasProperty(properties, "RiskType"))
			errors.Add(new ValidationError("Properties.RiskType", "Missing required field", "Error"));

		return errors;
	}

	private static bool HasProperty(JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out _);
	}
}
