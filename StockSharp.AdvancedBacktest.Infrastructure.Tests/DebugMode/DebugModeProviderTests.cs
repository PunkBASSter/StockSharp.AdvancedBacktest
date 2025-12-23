using StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

namespace StockSharp.AdvancedBacktest.Infrastructure.Tests.DebugMode;

public class DebugModeProviderTests
{
    [Fact]
    public void CaptureEvent_AuxiliaryTimeframe_FiltersOut()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = true,
            IsAiDebugEnabled = true,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var mockOutput = new MockDebugOutput();
        provider.SetHumanOutput(mockOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: true);

        // Assert: Event should be filtered out
        Assert.Empty(mockOutput.CapturedEvents);
    }

    [Fact]
    public void CaptureEvent_MainTimeframe_PassesThrough()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = true,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var mockOutput = new MockDebugOutput();
        provider.SetHumanOutput(mockOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: false);

        // Assert: Event should be captured
        Assert.Single(mockOutput.CapturedEvents);
    }

    [Fact]
    public void CaptureEvent_HumanDisabled_NoOutput()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = false,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var mockOutput = new MockDebugOutput();
        provider.SetHumanOutput(mockOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: false);

        // Assert: No event should be captured when disabled
        Assert.Empty(mockOutput.CapturedEvents);
    }

    [Fact]
    public void CaptureEvent_BothModesEnabled_BothReceive()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = true,
            IsAiDebugEnabled = true,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var humanOutput = new MockDebugOutput();
        var aiOutput = new MockDebugOutput();
        provider.SetHumanOutput(humanOutput);
        provider.SetAiOutput(aiOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: false);

        // Assert: Both should receive the event
        Assert.Single(humanOutput.CapturedEvents);
        Assert.Single(aiOutput.CapturedEvents);
    }

    [Fact]
    public void CaptureEvent_OnlyAiEnabled_OnlyAiReceives()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = false,
            IsAiDebugEnabled = true,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var humanOutput = new MockDebugOutput();
        var aiOutput = new MockDebugOutput();
        provider.SetHumanOutput(humanOutput);
        provider.SetAiOutput(aiOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: false);

        // Assert
        Assert.Empty(humanOutput.CapturedEvents);
        Assert.Single(aiOutput.CapturedEvents);
    }

    [Fact]
    public void CaptureEvent_TimestampRemappedCorrectly()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = true,
            MainTimeframe = TimeSpan.FromHours(1)
        };
        var mockOutput = new MockDebugOutput();
        provider.SetHumanOutput(mockOutput);

        var eventData = new { Type = "StopLoss", Price = 95m };
        // Event at 10:15 should display under 10:00
        var timestamp = new DateTimeOffset(2025, 1, 15, 10, 15, 0, TimeSpan.Zero);

        // Act
        provider.CaptureEvent(eventData, timestamp, isAuxiliaryTimeframe: false);

        // Assert: Timestamp should be remapped to hour boundary
        var captured = mockOutput.CapturedEvents.Single();
        Assert.Equal(new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero), captured.DisplayTimestamp);
    }

    [Fact]
    public void Dispose_CleansUpOutputs()
    {
        // Arrange
        var provider = new DebugModeProvider
        {
            IsHumanDebugEnabled = true,
            IsAiDebugEnabled = true
        };
        var humanOutput = new MockDebugOutput();
        var aiOutput = new MockDebugOutput();
        provider.SetHumanOutput(humanOutput);
        provider.SetAiOutput(aiOutput);

        // Act
        provider.Dispose();

        // Assert
        Assert.True(humanOutput.WasDisposed);
        Assert.True(aiOutput.WasDisposed);
    }
}

internal class MockDebugOutput : IDebugModeOutput
{
    public List<(object EventData, DateTimeOffset DisplayTimestamp)> CapturedEvents { get; } = new();
    public bool WasDisposed { get; private set; }
    public bool WasFlushed { get; private set; }

    public void Write(object eventData, DateTimeOffset displayTimestamp)
    {
        CapturedEvents.Add((eventData, displayTimestamp));
    }

    public void Flush()
    {
        WasFlushed = true;
    }

    public void Dispose()
    {
        WasDisposed = true;
    }
}
