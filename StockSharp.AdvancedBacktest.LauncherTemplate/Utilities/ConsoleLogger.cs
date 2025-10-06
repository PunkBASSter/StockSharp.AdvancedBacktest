namespace StockSharp.AdvancedBacktest.LauncherTemplate.Utilities;

/// <summary>
/// Provides thread-safe, color-coded console logging with timestamp support.
/// </summary>
public static class ConsoleLogger
{
    private static readonly object ConsoleLock = new();
    private static bool _progressActive;
    private static int _lastProgressLength;

    public static void LogInfo(string message)
    {
        Log(message, ConsoleColor.White, "INFO");
    }

    public static void LogSuccess(string message)
    {
        Log(message, ConsoleColor.Green, "SUCCESS");
    }

    public static void LogWarning(string message)
    {
        Log(message, ConsoleColor.Yellow, "WARNING");
    }

    public static void LogError(string message)
    {
        Log(message, ConsoleColor.Red, "ERROR");
    }

    public static void LogSection(string title)
    {
        lock (ConsoleLock)
        {
            ClearProgressIfActive();

            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;

                var separator = new string('=', 80);
                Console.WriteLine();
                Console.WriteLine(separator);

                var titleWithSpaces = $"  {title}  ";
                var padding = (80 - titleWithSpaces.Length) / 2;
                var centeredTitle = new string(' ', Math.Max(0, padding)) + titleWithSpaces;
                Console.WriteLine(centeredTitle);

                Console.WriteLine(separator);
                Console.WriteLine();
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    public static void ShowProgress(string message, int currentValue, int totalValue)
    {
        lock (ConsoleLock)
        {
            var percentage = totalValue > 0 ? (currentValue * 100) / totalValue : 0;
            var progressText = $"[{GetTimestamp()}] {message} ({currentValue}/{totalValue} - {percentage}%)";

            if (_progressActive)
            {
                ClearCurrentLine();
            }

            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(progressText);
                _lastProgressLength = progressText.Length;
                _progressActive = true;
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    public static void HideProgress()
    {
        lock (ConsoleLock)
        {
            if (_progressActive)
            {
                ClearCurrentLine();
                _progressActive = false;
                _lastProgressLength = 0;
            }
        }
    }

    private static void Log(string message, ConsoleColor color, string level)
    {
        if (string.IsNullOrEmpty(message))
            return;

        lock (ConsoleLock)
        {
            ClearProgressIfActive();

            var lines = message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var timestamp = GetTimestamp();
            var prefix = $"[{timestamp}] [{level}] ";

            var originalColor = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (i == 0)
                    {
                        Console.WriteLine($"{prefix}{lines[i]}");
                    }
                    else
                    {
                        var indent = new string(' ', prefix.Length);
                        Console.WriteLine($"{indent}{lines[i]}");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }

    private static string GetTimestamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    private static void ClearProgressIfActive()
    {
        if (_progressActive)
        {
            ClearCurrentLine();
            _progressActive = false;
            _lastProgressLength = 0;
        }
    }

    private static void ClearCurrentLine()
    {
        Console.Write('\r');
        Console.Write(new string(' ', _lastProgressLength));
        Console.Write('\r');
    }
}
