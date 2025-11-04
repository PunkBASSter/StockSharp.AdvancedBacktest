using System.Diagnostics;

namespace StockSharp.AdvancedBacktest.DebugMode;

public class DebugWebAppLauncher : IDisposable
{
    private readonly string _webProjectPath;
    private readonly string _serverUrl;
    private readonly string _debugPagePath;
    private Process? _devServerProcess;
    private bool _disposed;

    public DebugWebAppLauncher(string webProjectPath, string serverUrl = "http://localhost:3000", string debugPagePath = "/debug-mode")
    {
        if (string.IsNullOrWhiteSpace(webProjectPath))
            throw new ArgumentException("Web project path cannot be null or empty", nameof(webProjectPath));

        if (!Directory.Exists(webProjectPath))
            throw new DirectoryNotFoundException($"Web project directory not found: {webProjectPath}");

        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("Server URL cannot be null or empty", nameof(serverUrl));

        _webProjectPath = webProjectPath;
        _serverUrl = serverUrl.TrimEnd('/');
        _debugPagePath = debugPagePath.StartsWith('/') ? debugPagePath : $"/{debugPagePath}";

        // Register cleanup on app exit
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        CleanupDevServer();
    }

    /// <summary>
    /// Ensures the debug server is running and opens the debug page in browser.
    /// </summary>
    /// <returns>True if server is running or was started successfully, false otherwise</returns>
    public async Task<bool> EnsureServerRunningAndOpenAsync()
    {
        Console.WriteLine("=== Debug Mode Web Server ===");

        // Check if already running
        if (await IsServerRunningAsync())
        {
            Console.WriteLine($"✓ Debug server already running at {_serverUrl}");
            OpenDebugPage();
            return true;
        }

        // Launch server
        Console.WriteLine($"Starting debug server at {_webProjectPath}...");
        try
        {
            if (!LaunchDevServer())
            {
                Console.WriteLine("✗ Failed to launch dev server");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Failed to launch dev server: {ex.Message}");
            return false;
        }

        // Wait for ready
        Console.Write("Waiting for server to be ready");
        if (await WaitForServerAsync())
        {
            Console.WriteLine();
            Console.WriteLine($"✓ Debug server ready at {_serverUrl}");
            OpenDebugPage();
            return true;
        }

        Console.WriteLine();
        Console.WriteLine("✗ Server did not respond within timeout period");
        Console.WriteLine("Check the npm output above for errors");
        return false;
    }

    /// <summary>
    /// Opens the debug page in the default browser.
    /// </summary>
    public void OpenDebugPage()
    {
        var url = $"{_serverUrl}{_debugPagePath}";
        try
        {
            Console.WriteLine($"Opening debug page: {url}");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open browser: {ex.Message}");
            Console.WriteLine($"Please manually open: {url}");
        }
    }

    /// <summary>
    /// Checks if the debug server is responding to HTTP requests.
    /// Timeout and connection errors are expected and handled when server is not running.
    /// </summary>
    /// <returns>True if server responds with success status code, false otherwise</returns>
    private async Task<bool> IsServerRunningAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var response = await client.GetAsync(_serverUrl);
            return response.IsSuccessStatusCode;
        }
        catch (TaskCanceledException)
        {
            // Timeout is expected when server is not running
            return false;
        }
        catch (HttpRequestException)
        {
            // Connection refused/network errors are expected when server is not running
            return false;
        }
        catch (Exception)
        {
            // Any other exception means server is not accessible
            return false;
        }
    }

    private bool LaunchDevServer()
    {
        // Clean up any existing process we're tracking
        CleanupDevServer();

        // Kill any process using the port
        var port = new Uri(_serverUrl).Port;
        KillProcessOnPort(port);

        // Wait a moment for port to be released
        Thread.Sleep(500);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c npm run dev",
                WorkingDirectory = _webProjectPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start npm process - Process.Start returned null");
            }

            _devServerProcess = process;

            // Capture output asynchronously to detect startup issues
            var errorLines = new List<string>();
            var hasStartupError = false;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"[npm] {e.Data}");

                    // Detect common error patterns
                    if (e.Data.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("EADDRINUSE", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("port already in use", StringComparison.OrdinalIgnoreCase))
                    {
                        hasStartupError = true;
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorLines.Add(e.Data);
                    Console.WriteLine($"[npm ERROR] {e.Data}");
                    hasStartupError = true;
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Give process a moment to start and detect immediate failures
            Thread.Sleep(2000);

            if (process.HasExited)
            {
                var errorMessage = errorLines.Count > 0
                    ? string.Join(Environment.NewLine, errorLines)
                    : "Process exited immediately";

                throw new InvalidOperationException(
                    $"npm process exited with code {process.ExitCode}. Error: {errorMessage}");
            }

            if (hasStartupError && errorLines.Count > 0)
            {
                throw new InvalidOperationException(
                    $"npm process reported errors: {string.Join(Environment.NewLine, errorLines)}");
            }

            Console.WriteLine($"npm dev server process started (PID: {process.Id})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching dev server: {ex.Message}");
            CleanupDevServer();
            throw;
        }
    }

    private void KillProcessOnPort(int port)
    {
        try
        {
            Console.WriteLine($"Checking for processes on port {port}...");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"Get-NetTCPConnection -LocalPort {port} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | ForEach-Object {{ Stop-Process -Id $_ -Force }}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Cleaned up process on port {port}");
                }
            }
        }
        catch
        {
            // Ignore errors - port might not be in use
        }
    }

    private void CleanupDevServer()
    {
        if (_devServerProcess != null && !_devServerProcess.HasExited)
        {
            try
            {
                Console.WriteLine($"Stopping dev server process (PID: {_devServerProcess.Id})...");
                _devServerProcess.Kill(entireProcessTree: true);
                _devServerProcess.WaitForExit(5000);
                Console.WriteLine("Dev server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to stop dev server: {ex.Message}");
            }
            finally
            {
                _devServerProcess.Dispose();
                _devServerProcess = null;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        CleanupDevServer();
        _disposed = true;
    }

    private async Task<bool> WaitForServerAsync(int maxWaitSeconds = 30)
    {
        for (int i = 0; i < maxWaitSeconds; i++)
        {
            await Task.Delay(1000);
            Console.Write(".");

            if (await IsServerRunningAsync())
            {
                return true;
            }
        }
        return false;
    }
}
