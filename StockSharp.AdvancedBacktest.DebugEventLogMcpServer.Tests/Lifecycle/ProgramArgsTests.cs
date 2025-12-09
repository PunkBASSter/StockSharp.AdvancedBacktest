using StockSharp.AdvancedBacktest.DebugEventLogMcpServer;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Lifecycle;

public sealed class ProgramArgsTests
{
    [Theory]
    [InlineData(new string[] { "--shutdown" }, true, null)]
    [InlineData(new string[] { "--database", "C:\\test\\db.sqlite" }, false, "C:\\test\\db.sqlite")]
    [InlineData(new string[] { "--database", "C:\\test\\db.sqlite", "--shutdown" }, true, "C:\\test\\db.sqlite")]
    [InlineData(new string[] { }, false, null)]
    public void Parse_ExtractsArgumentsCorrectly(string[] args, bool expectedShutdown, string? expectedDatabase)
    {
        var parsed = ProgramArgs.Parse(args);

        Assert.Equal(expectedShutdown, parsed.Shutdown);
        Assert.Equal(expectedDatabase, parsed.DatabasePath);
    }

    [Fact]
    public void Parse_WithInvalidArg_IgnoresUnknown()
    {
        var parsed = ProgramArgs.Parse(["--unknown", "value"]);

        Assert.False(parsed.Shutdown);
        Assert.Null(parsed.DatabasePath);
    }
}
