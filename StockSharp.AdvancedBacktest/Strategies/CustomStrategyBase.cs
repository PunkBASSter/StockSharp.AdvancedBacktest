using Ecng.Collections;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.AdvancedBacktest.Export;
using StockSharp.AdvancedBacktest.DebugMode;
using System.Security.Cryptography;
using System.Text;

namespace StockSharp.AdvancedBacktest.Strategies;

/// <summary>
/// Interface for strategies that can export indicator data for visualization
/// </summary>
public interface IIndicatorExportable
{
    /// <summary>
    /// Returns list of indicator series with their complete calculation history
    /// </summary>
    List<IndicatorDataSeries> GetIndicatorSeries();
}

public abstract class CustomStrategyBase : Strategy, IIndicatorExportable
{
    public string Hash => $"{GetType().Name}V{Version}_{SecuritiesHash}_{ParamsHash}";
    public PerformanceMetrics? PerformanceMetrics { get; protected set; }
    public DateTimeOffset MetricWindowStart { get; set; }
    public DateTimeOffset MetricWindowEnd { get; set; }

    public virtual string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Indicator exporter service for extracting indicator data
    /// Set this property before calling GetIndicatorSeries()
    /// </summary>
    public IIndicatorExporter? IndicatorExporter { get; set; }

    /// <summary>
    /// Optional debug mode exporter for real-time event capture during backtest.
    /// When set, automatically captures candles, indicators, trades, and state changes to JSONL files.
    /// Enables live visualization of strategy execution in browser.
    /// </summary>
    public DebugModeExporter? DebugExporter { get; set; }

    /// <summary>
    /// Default implementation: automatically extracts all indicators from Strategy.Indicators collection
    /// Override this method to provide custom indicator export logic
    /// </summary>
    public virtual List<IndicatorDataSeries> GetIndicatorSeries()
    {
        var seriesList = new List<IndicatorDataSeries>();

        // Use injected exporter or create a default one
        var exporter = IndicatorExporter ?? new IndicatorExporter();

        // Automatically discover all indicators registered with the strategy
        foreach (var indicator in Indicators)
        {
            try
            {
                // Handle complex indicators (with multiple sub-indicators)
                var series = exporter.ExtractComplexIndicator(indicator);
                seriesList.AddRange(series);
            }
            catch (Exception ex)
            {
                // Log warning but don't fail the entire export
                this.LogWarning($"Failed to export indicator {indicator.Name}: {ex.Message}");
            }
        }

        return seriesList;
    }

    public virtual string ParamsHash
    {
        get
        {
            // Get deterministic string representation of parameters
            var paramsString = ParamsContainer.GenerateHash();

            // Generate SHA256 hash and take first 8 characters
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(paramsString));
            return Convert.ToHexString(hashBytes)[..8].ToLower();
        }
    }

    public virtual string SecuritiesHash
    {
        get
        {
            return string.Join(";", Securities
                .OrderBy(s => s.Key.Id)  // Deterministic ordering
                .Select(s => $"{s.Key.Id}={string.Join(",", s.Value)}"));
        }
    }

    public virtual Dictionary<Security, IEnumerable<TimeSpan>> Securities { get; set; } = new(new SecurityIdComparer());

    public CustomParamsContainer ParamsContainer { get; set; } = new(Enumerable.Empty<ICustomParam>());

    protected T GetParam<T>(string id) => ParamsContainer.Get<T>(id);

    public static T Create<T>(List<ICustomParam> paramSet) where T : CustomStrategyBase, new()
    {
        var strategy = new T();

        var secparams = paramSet.Where(p => p is SecurityParam)
            .Cast<SecurityParam>()
            .ToDictionary(sp => sp.Value.Key, sp => sp.Value.AsEnumerable());
        strategy.Securities.AddRange(secparams);

        var nonsecparams = paramSet.Where(p => p is not SecurityParam).ToList();
        strategy.ParamsContainer = new CustomParamsContainer(nonsecparams);

        return strategy;
    }

    //TODO handle more elegantly, now it serves as a temp param storage
    public List<ICustomParam> ParamsBackup { get; set; } = [];

    /// <summary>
    /// Called when strategy starts. Initializes debug mode if DebugExporter is configured.
    /// </summary>
    protected override void OnStarted(DateTimeOffset time)
    {
        base.OnStarted(time);

        // Initialize debug exporter if configured
        DebugExporter?.Initialize(this);
        DebugExporter?.SubscribeToIndicators(Indicators);

        // Subscribe to candle events for debug mode
        if (DebugExporter != null && DebugExporter.IsInitialized)
        {
            var connector = SafeGetConnector();
            connector.CandleReceived += OnCandleReceivedForDebug;
        }
    }

    /// <summary>
    /// Called when strategy stops. Performs debug mode cleanup.
    /// </summary>
    protected override void OnStopped()
    {
        // Unsubscribe from candle events
        if (DebugExporter != null)
        {
            try
            {
                var connector = SafeGetConnector();
                connector.CandleReceived -= OnCandleReceivedForDebug;
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        // Cleanup debug exporter
        DebugExporter?.Cleanup();

        base.OnStopped();
    }

    /// <summary>
    /// Handles candle received events for debug mode capture.
    /// </summary>
    private void OnCandleReceivedForDebug(Subscription subscription, ICandleMessage candle)
    {
        if (DebugExporter == null || !DebugExporter.IsInitialized)
            return;

        try
        {
            // Get security ID from subscription or use first security
            var securityId = subscription?.SecurityId ?? Securities.Keys.FirstOrDefault()?.ToSecurityId() ?? default;
            DebugExporter.CaptureCandle(candle, securityId);
        }
        catch (Exception ex)
        {
            this.LogError($"Error capturing candle for debug mode: {ex.Message}");
        }
    }
}
