namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public sealed class McpShutdownSignal : IDisposable
{
    private const string EventName = @"Global\StockSharp.McpServer.Shutdown";

    private readonly EventWaitHandle _handle;
    private readonly bool _isOwner;
    private bool _disposed;

    private McpShutdownSignal(EventWaitHandle handle, bool isOwner)
    {
        _handle = handle;
        _isOwner = isOwner;
    }

    public static McpShutdownSignal CreateForServer()
    {
        var handle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);
        return new McpShutdownSignal(handle, true);
    }

    public static McpShutdownSignal? OpenExisting()
    {
        try
        {
            var handle = EventWaitHandle.OpenExisting(EventName);
            return new McpShutdownSignal(handle, false);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return null;
        }
    }

    public void WaitForShutdown(CancellationToken ct)
    {
        WaitHandle.WaitAny([_handle, ct.WaitHandle]);
    }

    public void Signal() => _handle.Set();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _handle.Dispose();
    }
}
