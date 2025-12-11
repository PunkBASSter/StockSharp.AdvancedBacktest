namespace StockSharp.AdvancedBacktest.PerformanceValidation;

public class WalkForwardConfig
{
    public required TimeSpan WindowSize { get; init; }
    public required TimeSpan StepSize { get; init; }
    public required TimeSpan ValidationSize { get; init; }
    public WindowGenerationMode Mode { get; init; } = WindowGenerationMode.Anchored;

    public IEnumerable<(DateTimeOffset trainStart, DateTimeOffset trainEnd, DateTimeOffset testStart, DateTimeOffset testEnd)> GenerateWindows(
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (endDate <= startDate)
            yield break;

        var totalDuration = endDate - startDate;
        var minimumRequired = WindowSize + ValidationSize;

        if (totalDuration < minimumRequired)
            yield break;

        var currentStart = startDate;

        while (true)
        {
            DateTimeOffset trainStart;
            DateTimeOffset trainEnd;
            DateTimeOffset testStart;
            DateTimeOffset testEnd;

            if (Mode == WindowGenerationMode.Anchored)
            {
                trainStart = startDate;
                trainEnd = currentStart + WindowSize;
            }
            else // Rolling
            {
                trainStart = currentStart;
                trainEnd = currentStart + WindowSize;
            }

            testStart = trainEnd;
            testEnd = testStart + ValidationSize;

            if (testEnd > endDate)
                yield break;

            yield return (trainStart, trainEnd, testStart, testEnd);

            currentStart += StepSize;
        }
    }
}
