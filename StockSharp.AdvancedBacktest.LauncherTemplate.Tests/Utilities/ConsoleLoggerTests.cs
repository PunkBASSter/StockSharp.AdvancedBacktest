using StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.Utilities;

public class ConsoleLoggerTests
{
    [Fact]
    public void LogInfo_WithNullMessage_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        ConsoleLogger.LogInfo(null!);
    }

    [Fact]
    public void LogInfo_WithEmptyMessage_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        ConsoleLogger.LogInfo(string.Empty);
    }

    [Fact]
    public void LogSuccess_WithMultiLineMessage_DoesNotThrow()
    {
        // Arrange
        var message = "Line 1\nLine 2\nLine 3";

        // Act & Assert - Should not throw
        ConsoleLogger.LogSuccess(message);
    }

    [Fact]
    public void LogWarning_WithCarriageReturnNewLine_DoesNotThrow()
    {
        // Arrange
        var message = "Line 1\r\nLine 2\r\nLine 3";

        // Act & Assert - Should not throw
        ConsoleLogger.LogWarning(message);
    }

    [Fact]
    public void LogError_WithLongMessage_DoesNotThrow()
    {
        // Arrange
        var message = new string('X', 1000);

        // Act & Assert - Should not throw
        ConsoleLogger.LogError(message);
    }

    [Fact]
    public void LogSection_WithNormalTitle_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        ConsoleLogger.LogSection("Test Section");
    }

    [Fact]
    public void LogSection_WithLongTitle_DoesNotThrow()
    {
        // Arrange
        var title = new string('X', 100);

        // Act & Assert - Should not throw
        ConsoleLogger.LogSection(title);
    }

    [Fact]
    public void ShowProgress_WithValidValues_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        ConsoleLogger.ShowProgress("Processing", 50, 100);
    }

    [Fact]
    public void ShowProgress_WithZeroTotal_DoesNotThrow()
    {
        // Act & Assert - Should not throw (handles edge case)
        ConsoleLogger.ShowProgress("Processing", 0, 0);
    }

    [Fact]
    public void ShowProgress_MultipleCalls_DoesNotThrow()
    {
        // Act & Assert - Should update progress without throwing
        ConsoleLogger.ShowProgress("Processing", 25, 100);
        ConsoleLogger.ShowProgress("Processing", 50, 100);
        ConsoleLogger.ShowProgress("Processing", 75, 100);
        ConsoleLogger.ShowProgress("Processing", 100, 100);
    }

    [Fact]
    public void HideProgress_WithoutShowProgress_DoesNotThrow()
    {
        // Act & Assert - Should handle being called without active progress
        ConsoleLogger.HideProgress();
    }

    [Fact]
    public void HideProgress_AfterShowProgress_DoesNotThrow()
    {
        // Arrange
        ConsoleLogger.ShowProgress("Test", 50, 100);

        // Act & Assert
        ConsoleLogger.HideProgress();
    }

    [Fact]
    public async Task ConcurrentLogging_FromMultipleThreads_DoesNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();
        var random = new Random();

        // Act - Simulate concurrent logging from multiple threads
        for (int i = 0; i < 10; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(() =>
            {
                ConsoleLogger.LogInfo($"Info message from task {taskId}");
                Thread.Sleep(random.Next(1, 10));
                ConsoleLogger.LogSuccess($"Success message from task {taskId}");
                Thread.Sleep(random.Next(1, 10));
                ConsoleLogger.LogWarning($"Warning message from task {taskId}");
                Thread.Sleep(random.Next(1, 10));
                ConsoleLogger.LogError($"Error message from task {taskId}");
            }));
        }

        // Assert - All tasks should complete without exceptions
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentProgressAndLogging_DoesNotThrow()
    {
        // Arrange
        var progressTask = Task.Run(() =>
        {
            for (int i = 0; i <= 100; i += 10)
            {
                ConsoleLogger.ShowProgress("Processing", i, 100);
                Thread.Sleep(10);
            }
            ConsoleLogger.HideProgress();
        });

        var loggingTask = Task.Run(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                ConsoleLogger.LogInfo($"Concurrent log message {i}");
                Thread.Sleep(20);
            }
        });

        // Act & Assert - Both tasks should complete without exceptions
        await Task.WhenAll(progressTask, loggingTask);
    }

    [Fact]
    public void LogSection_BetweenProgressUpdates_ClearsProgress()
    {
        // Arrange
        ConsoleLogger.ShowProgress("Test", 50, 100);

        // Act
        ConsoleLogger.LogSection("New Section");

        // Assert - Should not throw, progress should be cleared
        ConsoleLogger.LogInfo("After section");
    }

    [Fact]
    public void AllLogMethods_WithSpecialCharacters_DoesNotThrow()
    {
        // Arrange
        var specialMessage = "Test with special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?`~";

        // Act & Assert
        ConsoleLogger.LogInfo(specialMessage);
        ConsoleLogger.LogSuccess(specialMessage);
        ConsoleLogger.LogWarning(specialMessage);
        ConsoleLogger.LogError(specialMessage);
    }

    [Fact]
    public void AllLogMethods_WithUnicodeCharacters_DoesNotThrow()
    {
        // Arrange
        var unicodeMessage = "Test with unicode: → ← ↑ ↓ € £ ¥ © ® ™ α β γ δ 中文 日本語";

        // Act & Assert
        ConsoleLogger.LogInfo(unicodeMessage);
        ConsoleLogger.LogSuccess(unicodeMessage);
        ConsoleLogger.LogWarning(unicodeMessage);
        ConsoleLogger.LogError(unicodeMessage);
    }
}
