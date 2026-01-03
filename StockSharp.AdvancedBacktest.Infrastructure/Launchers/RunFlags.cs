namespace StockSharp.AdvancedBacktest.Launchers;

/// <summary>
/// Flags for controlling strategy launcher behavior.
/// </summary>
[Flags]
public enum RunFlags
{
    /// <summary>
    /// No special flags - default execution mode.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enable AI agentic debug mode (uses SQLite event repository, disables web app launcher).
    /// </summary>
    AiDebug = 1 << 0,

    /// <summary>
    /// Enable visual debugging web app (starts the Next.js development server).
    /// </summary>
    VisualDebug = 1 << 1
}
