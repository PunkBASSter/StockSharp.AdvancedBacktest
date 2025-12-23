using StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

namespace StockSharp.AdvancedBacktest.Infrastructure.Tests.DebugMode;

public class TimestampRemapperTests
{
    [Fact]
    public void RemapToMainTimeframe_ExactBoundary_ReturnsSameTime()
    {
        // Arrange
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromHours(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert
        Assert.Equal(eventTime, result);
    }

    [Fact]
    public void RemapToMainTimeframe_MidHour_FloorsToHourBoundary()
    {
        // Arrange
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 15, 30, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromHours(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void RemapToMainTimeframe_AuxiliaryEvent_RemapsTo5MinBoundary()
    {
        // Arrange: Event at 10:07 with 5-minute main TF
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 7, 45, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromMinutes(5);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert: Should floor to 10:05
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 5, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void RemapToMainTimeframe_HourlyMainTF_5MinAuxiliaryEvent()
    {
        // Arrange: Event triggered at 10:15 (5-min aux), main TF is 1 hour
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 15, 0, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromHours(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert: Should display under 10:00 hourly candle
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void RemapToMainTimeframe_EndOfHour_FloorsToPreviousHour()
    {
        // Arrange: Event at 10:59:59
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 59, 59, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromHours(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void RemapToMainTimeframe_PreservesOffset()
    {
        // Arrange: Event with timezone offset
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.FromHours(3));
        var mainTimeframe = TimeSpan.FromHours(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert: Offset should be preserved
        Assert.Equal(TimeSpan.FromHours(3), result.Offset);
        Assert.Equal(10, result.Hour);
        Assert.Equal(0, result.Minute);
    }

    [Fact]
    public void RemapToMainTimeframe_DailyTimeframe()
    {
        // Arrange: Event at 14:30 with daily main TF
        var eventTime = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromDays(1);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert: Should floor to start of day
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void RemapToMainTimeframe_15MinTimeframe()
    {
        // Arrange: Event at 10:22 with 15-minute main TF
        var eventTime = new DateTimeOffset(2025, 1, 15, 10, 22, 0, TimeSpan.Zero);
        var mainTimeframe = TimeSpan.FromMinutes(15);

        // Act
        var result = TimestampRemapper.RemapToMainTimeframe(eventTime, mainTimeframe);

        // Assert: Should floor to 10:15
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 15, 0, TimeSpan.Zero), result);
    }
}
