namespace StockSharp.AdvancedBacktest.Tests;

public class NullDebugEventSinkTests
{
    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = NullDebugEventSink.Instance;
        var instance2 = NullDebugEventSink.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void Instance_ImplementsIDebugEventSink()
    {
        var instance = NullDebugEventSink.Instance;

        Assert.IsAssignableFrom<IDebugEventSink>(instance);
    }

    [Fact]
    public void LogEvent_DoesNotThrow()
    {
        var sink = NullDebugEventSink.Instance;

        // Should complete without throwing
        sink.LogEvent("Category", "EventType", new { Data = "test" });
    }

    [Fact]
    public void LogEvent_WithNullData_DoesNotThrow()
    {
        var sink = NullDebugEventSink.Instance;

        // Should complete without throwing
        sink.LogEvent("Category", "EventType", null!);
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        var sink = NullDebugEventSink.Instance;

        // Should complete without throwing
        sink.Flush();
    }
}

public class IDebugEventSinkContractTests
{
    [Fact]
    public void Interface_DefinesLogEventMethod()
    {
        var method = typeof(IDebugEventSink).GetMethod("LogEvent");

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(object), parameters[2].ParameterType);
    }

    [Fact]
    public void Interface_DefinesFlushMethod()
    {
        var method = typeof(IDebugEventSink).GetMethod("Flush");

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }
}
