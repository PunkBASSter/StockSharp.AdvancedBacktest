namespace StockSharp.AdvancedBacktest.Validation;

public class WalkForwardResult
{
	public int TotalWindows { get; init; }
	public required List<WindowResult> Windows { get; init; }

	public double WalkForwardEfficiency
	{
		get
		{
			if (Windows.Count == 0)
				return 0.0;

			var avgOOS = Windows.Average(w => w.TestingMetrics.TotalReturn);
			var avgIS = Windows.Average(w => w.TrainingMetrics.TotalReturn);

			if (avgIS == 0.0)
				return 0.0;

			return avgOOS / avgIS;
		}
	}

	public double Consistency
	{
		get
		{
			if (Windows.Count == 0)
				return 0.0;

			var testReturns = Windows.Select(w => w.TestingMetrics.TotalReturn).ToArray();
			var mean = testReturns.Average();
			var variance = testReturns.Sum(r => Math.Pow(r - mean, 2)) / testReturns.Length;

			return Math.Sqrt(variance);
		}
	}
}
