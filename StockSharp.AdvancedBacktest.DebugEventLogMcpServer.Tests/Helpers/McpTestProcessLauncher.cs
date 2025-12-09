using System.Diagnostics;

namespace StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests.Helpers;

public sealed class McpServerAlreadyRunningException(string message) : Exception(message)
{
}

public sealed class McpTestProcessLauncher : IAsyncDisposable
{
    private Process? _process;
    private bool _disposed;

    public Stream StandardInput => _process?.StandardInput.BaseStream
        ?? throw new InvalidOperationException("Process not started");

    public Stream StandardOutput => _process?.StandardOutput.BaseStream
        ?? throw new InvalidOperationException("Process not started");

    public StreamWriter StandardInputWriter => _process?.StandardInput
        ?? throw new InvalidOperationException("Process not started");

    public StreamReader StandardOutputReader => _process?.StandardOutput
        ?? throw new InvalidOperationException("Process not started");

    public bool HasExited => _process?.HasExited ?? true;

    public int? ExitCode => _process?.HasExited == true ? _process.ExitCode : null;

    public async Task StartAsync(string databasePath, CancellationToken ct = default)
    {
        var executablePath = GetMcpServerExecutablePath();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{executablePath}\" --database \"{databasePath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();

        // Give the server a moment to initialize
        await Task.Delay(500, ct);

        // Check if process exited immediately with an error
        if (_process.HasExited)
        {
            var stderr = await _process.StandardError.ReadToEndAsync(ct);

            // If another instance is running, throw a specific exception for Skip
            if (stderr.Contains("Another MCP server instance is already running"))
            {
                throw new McpServerAlreadyRunningException(
                    "Another MCP server instance is already running. " +
                    "Run E2E tests individually or ensure no other MCP server is running.");
            }

            throw new InvalidOperationException(
                $"MCP server process exited immediately with code {_process.ExitCode}. " +
                $"Error output: {stderr}");
        }
    }

    public async Task<bool> StopAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
            return true;

        try
        {
            // Close stdin to signal EOF
            _process.StandardInput.Close();

            // Wait for graceful exit
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            try
            {
                await _process.WaitForExitAsync(cts.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Force kill if graceful shutdown times out
                _process.Kill(entireProcessTree: true);
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> ReadErrorOutputAsync()
    {
        if (_process is null)
            return null;

        return await _process.StandardError.ReadToEndAsync();
    }

    private static string GetMcpServerExecutablePath()
    {
        // Navigate from the test project's output to the MCP server executable
        // Tests run from: Tests/bin/Debug/net8.0/
        // Server is at: DebugEventLogMcpServer/bin/Debug/net8.0/

        var testAssemblyLocation = typeof(McpTestProcessLauncher).Assembly.Location;
        var testBinDir = Path.GetDirectoryName(testAssemblyLocation)!;

        // Go up to the solution root and then to the MCP server output
        var solutionRoot = Path.GetFullPath(Path.Combine(testBinDir, "..", "..", "..", ".."));
        var serverPath = Path.Combine(
            solutionRoot,
            "StockSharp.AdvancedBacktest.DebugEventLogMcpServer",
            "bin",
            "Debug",
            "net8.0",
            "StockSharp.AdvancedBacktest.DebugEventLogMcpServer.dll"
        );

        if (!File.Exists(serverPath))
        {
            throw new FileNotFoundException(
                $"MCP server executable not found. Please build the solution first. Expected path: {serverPath}");
        }

        return serverPath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_process is not null)
        {
            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            _process.Dispose();

            // Wait for mutex to be fully released
            await Task.Delay(200);
        }
    }
}
