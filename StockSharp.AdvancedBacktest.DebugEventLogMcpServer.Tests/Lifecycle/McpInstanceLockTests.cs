using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Lifecycle;

public sealed class McpInstanceLockTests
{
    private static string UniqueTestMutexName() => $"Local\\Test.McpServer.{Guid.NewGuid():N}";

    [Fact]
    public void TryAcquire_WhenNotHeld_ReturnsTrue()
    {
        using var instanceLock = new McpInstanceLock(UniqueTestMutexName());
        var acquired = instanceLock.TryAcquire();

        Assert.True(acquired);
    }

    [Fact]
    public async Task TryAcquire_WhenAlreadyHeld_ReturnsFalse()
    {
        var mutexName = UniqueTestMutexName();
        using var holderReady = new ManualResetEventSlim();
        using var holderDone = new ManualResetEventSlim();

        var holderTask = Task.Run(() =>
        {
            using var firstLock = new McpInstanceLock(mutexName);
            firstLock.TryAcquire();
            holderReady.Set();
            holderDone.Wait();
        });

        holderReady.Wait();

        using var secondLock = new McpInstanceLock(mutexName);
        var secondAcquired = secondLock.TryAcquire();

        holderDone.Set();
        await holderTask;

        Assert.False(secondAcquired);
    }

    [Fact]
    public void IsAnotherInstanceRunning_WhenNoneRunning_ReturnsFalse()
    {
        using var instanceLock = new McpInstanceLock(UniqueTestMutexName());
        var isRunning = instanceLock.IsAnotherInstanceRunning();

        Assert.False(isRunning);
    }

    [Fact]
    public async Task IsAnotherInstanceRunning_WhenAnotherHoldsLock_ReturnsTrue()
    {
        var mutexName = UniqueTestMutexName();
        using var holderReady = new ManualResetEventSlim();
        using var holderDone = new ManualResetEventSlim();

        var holderTask = Task.Run(() =>
        {
            using var firstLock = new McpInstanceLock(mutexName);
            firstLock.TryAcquire();
            holderReady.Set();
            holderDone.Wait();
        });

        holderReady.Wait();

        using var secondLock = new McpInstanceLock(mutexName);
        var isRunning = secondLock.IsAnotherInstanceRunning();

        holderDone.Set();
        await holderTask;

        Assert.True(isRunning);
    }

    [Fact]
    public void Dispose_ReleasesMutex_AllowingNewAcquisition()
    {
        var mutexName = UniqueTestMutexName();
        var firstLock = new McpInstanceLock(mutexName);
        firstLock.TryAcquire();
        firstLock.Dispose();

        using var secondLock = new McpInstanceLock(mutexName);
        var acquired = secondLock.TryAcquire();

        Assert.True(acquired);
    }

    [Fact]
    public void TryAcquire_AfterAbandonedMutex_RecoversAndAcquires()
    {
        var mutexName = UniqueTestMutexName();

        var holder = new Mutex(true, mutexName, out var createdNew);
        Assert.True(createdNew);

        // Simulate process crash - don't release, just dispose
        holder.Dispose();

        using var instanceLock = new McpInstanceLock(mutexName);
        var acquired = instanceLock.TryAcquire();

        Assert.True(acquired);
    }
}
