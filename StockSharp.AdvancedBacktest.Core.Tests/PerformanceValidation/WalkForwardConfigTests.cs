using StockSharp.AdvancedBacktest.PerformanceValidation;

namespace StockSharp.AdvancedBacktest.Tests.PerformanceValidation;

public class WalkForwardConfigTests
{
    [Theory]
    [InlineData(-10)] // end before start
    [InlineData(20)]  // period too short for window + validation
    public void GenerateWindows_ReturnsEmpty_WhenInvalid(int daysToAdd)
    {
        var config = CreateConfig();
        var startDate = DateTimeOffset.Now;
        var endDate = startDate.AddDays(daysToAdd);

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.Empty(windows);
    }

    [Theory]
    [InlineData(WindowGenerationMode.Anchored, true)]  // anchored keeps constant train start
    [InlineData(WindowGenerationMode.Rolling, false)] // rolling advances train start
    public void GenerateWindows_ModeBehavior(WindowGenerationMode mode, bool expectConstantTrainStart)
    {
        var config = CreateConfig(mode);
        var startDate = DateTimeOffset.Now;
        var endDate = startDate.AddDays(60);

        var windows = config.GenerateWindows(startDate, endDate).ToList();

        Assert.NotEmpty(windows);
        if (windows.Count > 1)
        {
            var trainStartsEqual = windows[0].trainStart == windows[1].trainStart;
            Assert.Equal(expectConstantTrainStart, trainStartsEqual);
        }
    }

    private static WalkForwardConfig CreateConfig(WindowGenerationMode mode = WindowGenerationMode.Anchored) => new()
    {
        WindowSize = TimeSpan.FromDays(30),
        StepSize = TimeSpan.FromDays(7),
        ValidationSize = TimeSpan.FromDays(7),
        Mode = mode
    };
}
