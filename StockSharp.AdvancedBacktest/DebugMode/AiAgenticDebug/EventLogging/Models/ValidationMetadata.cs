using System.Text.Json;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Serialization;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

public sealed record ValidationError(
	string Field,
	string Error,
	string Severity
);

public sealed class ValidationMetadata
{
	public required IReadOnlyList<ValidationError> Errors { get; init; }

	public bool HasErrors => Errors.Any(e => e.Severity == "Error");
	public bool HasWarnings => Errors.Any(e => e.Severity == "Warning");

	public string ToJson() => JsonSerializer.Serialize(Errors, EventJsonContext.Default.ListValidationError);

	public static ValidationMetadata? FromJson(string? json)
	{
		if (string.IsNullOrEmpty(json)) return null;

		var errors = JsonSerializer.Deserialize(json, EventJsonContext.Default.ListValidationError);
		return errors != null ? new ValidationMetadata { Errors = errors } : null;
	}
}
