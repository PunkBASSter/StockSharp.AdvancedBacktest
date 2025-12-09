namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public sealed class McpInstanceLock : IMcpInstanceLock
{
    private const string DefaultMutexName = @"Global\StockSharp.McpServer.Lock";

    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _acquired;
    private bool _disposed;

    public McpInstanceLock() : this(DefaultMutexName) { }

    internal McpInstanceLock(string mutexName)
    {
        _mutexName = mutexName;
    }

    public bool TryAcquire()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McpInstanceLock));

        if (_acquired)
            return true;

        _mutex = new Mutex(false, _mutexName, out _);

        try
        {
            _acquired = _mutex.WaitOne(0);
            return _acquired;
        }
        catch (AbandonedMutexException)
        {
            _acquired = true;
            return true;
        }
    }

    public bool IsAnotherInstanceRunning()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McpInstanceLock));

        if (_acquired)
            return false;

        try
        {
            using var testMutex = new Mutex(false, _mutexName, out _);
            try
            {
                var canAcquire = testMutex.WaitOne(0);
                if (canAcquire)
                {
                    testMutex.ReleaseMutex();
                    return false;
                }
                return true;
            }
            catch (AbandonedMutexException)
            {
                testMutex.ReleaseMutex();
                return false;
            }
        }
        catch
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_mutex is not null)
        {
            if (_acquired)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Mutex not owned - ignore
                }
            }
            _mutex.Dispose();
        }
    }
}
