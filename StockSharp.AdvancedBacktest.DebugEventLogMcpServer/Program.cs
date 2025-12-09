using StockSharp.AdvancedBacktest.DebugEventLogMcpServer;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

var parsedArgs = ProgramArgs.Parse(args);

if (parsedArgs.Shutdown)
{
    var signaled = ShutdownHandler.TrySignalShutdown();
    return signaled ? 0 : 1;
}

using var instanceLock = new McpInstanceLock();
if (!instanceLock.TryAcquire())
{
    Console.Error.WriteLine("Another MCP server instance is already running.");
    return 1;
}

await ServerStartup.RunAsync(args, parsedArgs.DatabasePath, CancellationToken.None);
return 0;
