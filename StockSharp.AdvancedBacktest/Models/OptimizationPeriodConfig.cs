using System;

namespace StockSharp.AdvancedBacktest.Models;

public class OptimizationPeriodConfig
{
	public const string SectionName = nameof(OptimizationPeriodConfig);
	public DateTimeOffset TrainingStartDate { get; set; }
	public DateTimeOffset TrainingEndDate { get; set; }
	public DateTimeOffset ValidationStartDate { get; set; }
	public DateTimeOffset ValidationEndDate { get; set; }
	public bool IsValid() => TrainingStartDate < TrainingEndDate
		&& ValidationStartDate < ValidationEndDate
		&& TrainingEndDate < ValidationStartDate;

	public void CreateSlidingWindow(TimeSpan trainingSize, TimeSpan validationSize, DateTimeOffset? lastDate = null)
	{
		lastDate ??= DateTimeOffset.UtcNow;
		if (trainingSize <= TimeSpan.Zero || validationSize <= TimeSpan.Zero)
			throw new ArgumentException("Training and validation sizes must be greater than zero.");

		TrainingStartDate = lastDate.Value - trainingSize - validationSize;
		TrainingEndDate = lastDate.Value - validationSize;
		ValidationStartDate = TrainingEndDate;
		ValidationEndDate = lastDate.Value;
	}
}
