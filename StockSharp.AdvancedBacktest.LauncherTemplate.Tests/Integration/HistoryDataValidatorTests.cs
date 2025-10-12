using StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;
using StockSharp.AdvancedBacktest.Tests.Fixtures;

namespace StockSharp.AdvancedBacktest.Tests.Integration;

[Trait("Category", "Integration")]
public class HistoryDataValidatorTests : IClassFixture<HistoryDataFixture>
{
    private readonly HistoryDataFixture _fixture;

    public HistoryDataValidatorTests(HistoryDataFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Validate_WithValidPath_ReturnsSuccess()
    {
        var validator = new HistoryDataValidator(_fixture.TestDataPath);
        var timeFrames = new List<TimeSpan> { TimeSpan.FromDays(1) };
        var securities = new List<string> { _fixture.MockSecurityId };

        var report = validator.Validate(securities, timeFrames);

        Assert.True(report.IsSuccess);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void Validate_WithInvalidPath_ReturnsError()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid()}");
        var validator = new HistoryDataValidator(invalidPath);
        var timeFrames = new List<TimeSpan> { TimeSpan.FromDays(1) };
        var securities = new List<string> { "BTCUSDT@BNB" };

        var report = validator.Validate(securities, timeFrames);

        Assert.False(report.IsSuccess);
        Assert.Contains(report.Errors, e => e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_WithMissingSecurities_ReturnsWarning()
    {
        var validator = new HistoryDataValidator(_fixture.TestDataPath);
        var timeFrames = new List<TimeSpan> { TimeSpan.FromDays(1) };
        var securities = new List<string> { "NONEXISTENT@BNB" };

        var report = validator.Validate(securities, timeFrames);

        Assert.Contains(report.Warnings, w => w.Contains("No data available"));
    }

    [Fact]
    public void Validate_WithMissingTimeframe_ReturnsWarning()
    {
        var validator = new HistoryDataValidator(_fixture.TestDataPath);
        var timeFrames = new List<TimeSpan> { TimeSpan.FromHours(4) };
        var securities = new List<string> { _fixture.MockSecurityId };

        var report = validator.Validate(securities, timeFrames);

        Assert.Contains(report.Warnings, w => w.Contains("No data available"));
    }

    [Fact]
    public void Constructor_WithNullPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new HistoryDataValidator(null!));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new HistoryDataValidator(string.Empty));
    }

    [Fact]
    public void ValidationReport_PrintToConsole_DoesNotThrow()
    {
        var validator = new HistoryDataValidator(_fixture.TestDataPath);
        var timeFrames = new List<TimeSpan> { TimeSpan.FromDays(1) };
        var securities = new List<string> { _fixture.MockSecurityId };

        var report = validator.Validate(securities, timeFrames);

        var exception = Record.Exception(() => report.PrintToConsole());

        Assert.Null(exception);
    }
}
