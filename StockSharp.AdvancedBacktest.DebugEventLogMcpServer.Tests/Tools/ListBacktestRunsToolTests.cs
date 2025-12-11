using System.Text.Json;
using Microsoft.Data.Sqlite;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Models;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.EventLogging.Storage;
using StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer.Tools;
using Xunit;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Tools;

public sealed class ListBacktestRunsToolTests : IAsyncDisposable
{
	private readonly SqliteConnection _connection;
	private readonly SqliteEventRepository _repository;
	private readonly ListBacktestRunsTool _tool;

	public ListBacktestRunsToolTests()
	{
		_connection = new SqliteConnection("Data Source=:memory:");
		_connection.Open();
		DatabaseSchema.InitializeAsync(_connection).Wait();
		_repository = new SqliteEventRepository(_connection);
		_tool = new ListBacktestRunsTool(_repository);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(20)]
	public async Task ListBacktestRunsAsync_WithVariousRunCounts_ShouldReturnCorrectCount(int runCount)
	{
		for (int i = 0; i < runCount; i++)
			await CreateTestRunAsync();

		var result = await _tool.ListBacktestRunsAsync();

		using var doc = JsonDocument.Parse(result);
		var runs = doc.RootElement.GetProperty("runs");
		var totalCount = doc.RootElement.GetProperty("totalCount").GetInt32();

		Assert.Equal(runCount, runs.GetArrayLength());
		Assert.Equal(runCount, totalCount);
	}

	[Fact]
	public async Task ListBacktestRunsAsync_ShouldReturnRunsOrderedByCreatedAtDescending()
	{
		var baseTime = DateTime.UtcNow;
		var runId1 = Guid.NewGuid().ToString();
		var runId2 = Guid.NewGuid().ToString();
		var runId3 = Guid.NewGuid().ToString();

		await CreateTestRunWithCreatedAtAsync(runId1, baseTime.AddMinutes(-2));
		await CreateTestRunWithCreatedAtAsync(runId2, baseTime.AddMinutes(-1));
		await CreateTestRunWithCreatedAtAsync(runId3, baseTime);

		var result = await _tool.ListBacktestRunsAsync();

		using var doc = JsonDocument.Parse(result);
		var runs = doc.RootElement.GetProperty("runs");

		Assert.Equal(3, runs.GetArrayLength());
		Assert.Equal(runId3, runs[0].GetProperty("id").GetString());
		Assert.Equal(runId2, runs[1].GetProperty("id").GetString());
		Assert.Equal(runId1, runs[2].GetProperty("id").GetString());
	}

	[Fact]
	public async Task ListBacktestRunsAsync_ShouldReturnCorrectStructureAndData()
	{
		var startTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
		var endTime = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);
		var configHash = "abc123def456";
		var runId = Guid.NewGuid().ToString();

		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = runId,
			StartTime = startTime,
			EndTime = endTime,
			StrategyConfigHash = configHash
		});

		var result = await _tool.ListBacktestRunsAsync();

		using var doc = JsonDocument.Parse(result);
		var runs = doc.RootElement.GetProperty("runs");
		Assert.Equal(1, runs.GetArrayLength());

		var firstRun = runs[0];

		// Verify all required properties exist and have correct values
		Assert.Equal(runId, firstRun.GetProperty("id").GetString());
		Assert.Equal(configHash, firstRun.GetProperty("strategyConfigHash").GetString());

		var startTimeStr = firstRun.GetProperty("startTime").GetString();
		var endTimeStr = firstRun.GetProperty("endTime").GetString();
		var createdAtStr = firstRun.GetProperty("createdAt").GetString();

		Assert.NotNull(startTimeStr);
		Assert.NotNull(endTimeStr);
		Assert.NotNull(createdAtStr);
		Assert.True(DateTime.TryParse(startTimeStr, out _));
		Assert.True(DateTime.TryParse(endTimeStr, out _));
		Assert.True(DateTime.TryParse(createdAtStr, out _));
	}

	private async Task<string> CreateTestRunAsync()
	{
		var runId = Guid.NewGuid().ToString();
		await _repository.CreateBacktestRunAsync(new BacktestRunEntity
		{
			Id = runId,
			StartTime = DateTime.UtcNow,
			EndTime = DateTime.UtcNow.AddHours(1),
			StrategyConfigHash = new string('a', 64)
		});
		return runId;
	}

	private async Task CreateTestRunWithCreatedAtAsync(string runId, DateTime createdAt)
	{
		using var command = _connection.CreateCommand();
		command.CommandText = @"
			INSERT INTO BacktestRuns (Id, StartTime, EndTime, StrategyConfigHash, CreatedAt)
			VALUES (@id, @startTime, @endTime, @configHash, @createdAt)";
		command.Parameters.AddWithValue("@id", runId);
		command.Parameters.AddWithValue("@startTime", DateTime.UtcNow.ToString("o"));
		command.Parameters.AddWithValue("@endTime", DateTime.UtcNow.AddHours(1).ToString("o"));
		command.Parameters.AddWithValue("@configHash", new string('a', 64));
		command.Parameters.AddWithValue("@createdAt", createdAt.ToString("o"));
		await command.ExecuteNonQueryAsync();
	}

	public async ValueTask DisposeAsync()
	{
		await _connection.DisposeAsync();
	}
}
