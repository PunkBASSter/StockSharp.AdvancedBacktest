using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Validation;

public static class CircularReferenceDetector
{
	public static bool IsSelfReference(EventEntity entity)
	{
		ArgumentNullException.ThrowIfNull(entity);

		if (string.IsNullOrEmpty(entity.ParentEventId))
			return false;

		return string.Equals(entity.EventId, entity.ParentEventId, StringComparison.OrdinalIgnoreCase);
	}

	public static void ThrowIfSelfReference(EventEntity entity)
	{
		if (IsSelfReference(entity))
		{
			throw new InvalidOperationException(
				$"Circular reference detected: Event '{entity.EventId}' cannot reference itself as parent.");
		}
	}
}
