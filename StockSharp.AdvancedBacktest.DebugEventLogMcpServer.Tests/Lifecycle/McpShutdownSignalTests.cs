using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;
using Xunit;
using Xunit.Sdk;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Lifecycle;

public sealed class McpShutdownSignalTests
{
    [Fact]
    public void CreateForServer_CreatesValidHandle()
    {
        using var signal = McpShutdownSignal.CreateForServer();
        Assert.NotNull(signal);
    }

    [SkippableFact]
    public void OpenExisting_ReturnsNull_WhenNoServerRunning()
    {
        var signal = McpShutdownSignal.OpenExisting();
        Skip.If(signal is not null, "Another MCP server instance is already running. Skip this test.");
        Assert.Null(signal);
    }

    [Fact]
    public void OpenExisting_ReturnsHandle_WhenServerSignalExists()
    {
        using var serverSignal = McpShutdownSignal.CreateForServer();
        using var clientSignal = McpShutdownSignal.OpenExisting();

        Assert.NotNull(clientSignal);
    }

    [Fact]
    public void Signal_UnblocksWaitForShutdown()
    {
        using var cts = new CancellationTokenSource();
        using var serverSignal = McpShutdownSignal.CreateForServer();

        var waitTask = Task.Run(() => serverSignal.WaitForShutdown(cts.Token));

        using var clientSignal = McpShutdownSignal.OpenExisting();
        clientSignal!.Signal();

        var completed = waitTask.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "WaitForShutdown should have been unblocked by Signal");
    }

    [Fact]
    public void WaitForShutdown_RespondsToCancel()
    {
        using var cts = new CancellationTokenSource();
        using var serverSignal = McpShutdownSignal.CreateForServer();

        var waitTask = Task.Run(() => serverSignal.WaitForShutdown(cts.Token));

        cts.Cancel();

        var completed = waitTask.Wait(TimeSpan.FromSeconds(1));
        Assert.True(completed, "WaitForShutdown should respond to cancellation");
    }
}
