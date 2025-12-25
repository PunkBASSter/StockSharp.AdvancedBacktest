namespace StockSharp.AdvancedBacktest.LauncherTemplate.Launchers;

/// <summary>
/// Abstraction for strategy launchers enabling DI resolution and CLI selection.
/// </summary>
public interface IStrategyLauncher
{
    /// <summary>
    /// Display name for CLI identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute backtest with the configured strategy.
    /// </summary>
    /// <param name="aiDebug">Enable AI agentic debug mode</param>
    /// <returns>Exit code (0 = success, non-zero = failure)</returns>
    Task<int> RunAsync(bool aiDebug);
}
