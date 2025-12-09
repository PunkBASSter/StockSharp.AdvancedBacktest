using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.E2E;

[CollectionDefinition("MCP E2E Tests", DisableParallelization = true)]
public sealed class McpE2ETestCollection : ICollectionFixture<McpE2ETestFixture>
{
}

public sealed class McpE2ETestFixture
{
    // Empty fixture - just used to disable parallelization for E2E tests
}
