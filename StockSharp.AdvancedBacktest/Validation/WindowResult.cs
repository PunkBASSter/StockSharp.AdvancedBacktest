using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Validation;

public class WindowResult
{
	public int WindowNumber { get; init; }
	public required PerformanceMetrics TrainingMetrics { get; init; }
	public required PerformanceMetrics TestingMetrics { get; init; }
	public required (DateTimeOffset start, DateTimeOffset end) TrainingPeriod { get; init; }
	public required (DateTimeOffset start, DateTimeOffset end) TestingPeriod { get; init; }

	public double PerformanceDegradation
	{
		get
		{
			if (TrainingMetrics.TotalReturn == 0.0)
				return 0.0;

			return (TestingMetrics.TotalReturn - TrainingMetrics.TotalReturn) / TrainingMetrics.TotalReturn;
		}
	}
}
