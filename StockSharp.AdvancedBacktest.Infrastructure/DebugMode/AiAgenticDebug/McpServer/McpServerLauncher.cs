using System.Diagnostics;

namespace StockSharp.AdvancedBacktest.DebugMode.AiAgenticDebug.McpServer;

public static class McpServerLauncher
{
    private const string ExeName = "StockSharp.AdvancedBacktest.DebugEventLogMcpServer";

    public static bool EnsureRunning(string? databasePath = null)
    {
        using var checkLock = new McpInstanceLock();
        if (checkLock.IsAnotherInstanceRunning())
            return true;

        var exePath = FindExecutable();
        if (exePath is null)
            return false;

        var arguments = databasePath is not null
            ? $"--database \"{databasePath}\""
            : string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        try
        {
            var process = Process.Start(startInfo);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    public static bool Shutdown()
    {
        using var signal = McpShutdownSignal.OpenExisting();
        if (signal is null)
            return false;

        signal.Signal();
        return true;
    }

    private static string? FindExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, $"{ExeName}.exe"),
            Path.Combine(baseDir, ExeName),
            Path.Combine(baseDir, "..", ExeName, "bin", "Debug", "net8.0", $"{ExeName}.exe"),
            Path.Combine(baseDir, "..", ExeName, "bin", "Release", "net8.0", $"{ExeName}.exe")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }
}
