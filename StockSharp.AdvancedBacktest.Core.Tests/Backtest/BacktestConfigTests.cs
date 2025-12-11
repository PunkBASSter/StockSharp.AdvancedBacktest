using StockSharp.AdvancedBacktest.Backtest;

namespace StockSharp.AdvancedBacktest.Tests.Backtest;

public class PeriodConfigTests
{
    [Theory]
    [InlineData(-30, 0, true)]   // start before end
    [InlineData(0, -30, false)]  // start after end
    public void IsValid_ReturnsExpected(int startDaysOffset, int endDaysOffset, bool expected)
    {
        var config = new PeriodConfig
        {
            StartDate = DateTimeOffset.Now.AddDays(startDaysOffset),
            EndDate = DateTimeOffset.Now.AddDays(endDaysOffset)
        };

        Assert.Equal(expected, config.IsValid());
    }
}
