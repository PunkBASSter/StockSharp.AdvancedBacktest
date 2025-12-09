using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Integration;

public interface IEventSink : IAsyncDisposable
{
	Task InitializeAsync(string runId);
	Task WriteEventAsync(EventEntity entity);
	Task FlushAsync();
}
