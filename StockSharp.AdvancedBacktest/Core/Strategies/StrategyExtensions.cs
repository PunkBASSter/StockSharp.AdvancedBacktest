using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Extension methods for StockSharp Strategy integration with enhanced features
/// </summary>
public static class StrategyExtensions
{
    #region Strategy Configuration

    /// <summary>
    /// Configure enhanced strategy with dependency injection
    /// </summary>
    /// <param name="strategy">Strategy to configure</param>
    /// <param name="serviceProvider">Service provider</param>
    /// <returns>Configured strategy</returns>
    public static T ConfigureEnhanced<T>(this T strategy, IServiceProvider serviceProvider)
        where T : Strategy, IEnhancedStrategy
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        // Initialize enhanced features asynchronously
        _ = Task.Run(async () => await strategy.InitializeAsync(serviceProvider));

        return strategy;
    }

    /// <summary>
    /// Add enhanced parameters to strategy
    /// </summary>
    /// <param name="strategy">Strategy to configure</param>
    /// <param name="parameterDefinitions">Parameter definitions</param>
    /// <returns>Configured strategy</returns>
    public static T WithParameters<T>(this T strategy, params ParameterDefinition[] parameterDefinitions)
        where T : Strategy, IEnhancedStrategy
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));
        if (parameterDefinitions == null)
            throw new ArgumentNullException(nameof(parameterDefinitions));

        // Add parameters to the enhanced parameter set
        foreach (var definition in parameterDefinitions)
        {
            // Implementation would depend on the specific IParameterSet implementation
            // This is a placeholder for the actual parameter addition logic
        }

        return strategy;
    }

    /// <summary>
    /// Configure risk management for strategy
    /// </summary>
    /// <param name="strategy">Strategy to configure</param>
    /// <param name="configure">Risk configuration action</param>
    /// <returns>Configured strategy</returns>
    public static T WithRiskManagement<T>(this T strategy, Action<Interfaces.IRiskManager> configure)
        where T : Strategy, IEnhancedStrategy
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        if (strategy.RiskManager != null && strategy.RiskManager is Interfaces.IRiskManager enhancedRiskManager)
        {
            configure(enhancedRiskManager);
        }

        return strategy;
    }

    #endregion

    #region Portfolio Extensions

    /// <summary>
    /// Get enhanced portfolio snapshot from StockSharp portfolio
    /// </summary>
    /// <param name="portfolio">StockSharp portfolio</param>
    /// <returns>Portfolio snapshot</returns>
    public static PortfolioSnapshot ToSnapshot(this Portfolio portfolio)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));

        return new PortfolioSnapshot(
            TotalValue: portfolio.CurrentValue ?? 0m,
            Cash: portfolio.CurrentValue ?? 0m, // Simplified - would need proper cash calculation
            UnrealizedPnL: portfolio.UnrealizedPnL ?? 0m,
            RealizedPnL: portfolio.RealizedPnL ?? 0m,
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// Get current portfolio metrics
    /// </summary>
    /// <param name="portfolio">StockSharp portfolio</param>
    /// <returns>Portfolio metrics</returns>
    public static PortfolioMetrics GetMetrics(this Portfolio portfolio)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));

        var positions = 0; // Portfolio positions would need to be tracked separately
        var totalValue = portfolio.CurrentValue ?? 0m;
        var leverage = portfolio.Leverage ?? 1m;

        return new PortfolioMetrics(
            TotalPositions: positions,
            TotalValue: totalValue,
            Leverage: leverage,
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    #endregion

    #region Order Extensions

    /// <summary>
    /// Create enhanced trade execution data from StockSharp trade
    /// </summary>
    /// <param name="trade">StockSharp trade</param>
    /// <param name="parameters">Strategy parameters</param>
    /// <param name="portfolioSnapshot">Portfolio snapshot</param>
    /// <returns>Trade execution data</returns>
    public static TradeExecutionData ToExecutionData(
        this Trade trade,
        ImmutableDictionary<string, object?> parameters,
        PortfolioSnapshot portfolioSnapshot)
    {
        if (trade == null)
            throw new ArgumentNullException(nameof(trade));
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));

        return new TradeExecutionData(
            OriginalTrade: trade,
            StrategyParameters: parameters,
            Timestamp: trade.Time,
            PortfolioSnapshot: portfolioSnapshot
        );
    }

    /// <summary>
    /// Validate order against enhanced risk rules
    /// </summary>
    /// <param name="order">Order to validate</param>
    /// <param name="riskManager">Risk manager</param>
    /// <returns>Validation result</returns>
    public static ValidationResult ValidateRisk(this Order order, Interfaces.IRiskManager riskManager)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        if (riskManager == null)
            return ValidationResult.CreateSuccess();

        var errors = new List<string>();

        // Validate order
        if (!riskManager.ValidateOrder(order))
        {
            errors.Add($"Order {order.Id} failed risk validation");
        }

        // Validate position size
        if (order.Security != null && !riskManager.ValidatePositionSize(order.Security, order.Volume))
        {
            errors.Add($"Position size {order.Volume} exceeds limits for {order.Security.Code}");
        }

        return errors.Count == 0 ? ValidationResult.CreateSuccess() : ValidationResult.Failure(errors);
    }

    #endregion

    #region Performance Extensions

    /// <summary>
    /// Calculate strategy performance metrics
    /// </summary>
    /// <param name="strategy">Strategy instance</param>
    /// <returns>Performance metrics</returns>
    public static PerformanceMetrics CalculatePerformance(this Strategy strategy)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));

        var portfolio = strategy.Portfolio;
        if (portfolio == null)
        {
            return PerformanceMetrics.Empty;
        }

        var totalReturn = (portfolio.CurrentValue ?? 0m) / Math.Max(portfolio.BeginValue ?? 1m, 1m) - 1m;
        var unrealizedPnL = portfolio.UnrealizedPnL ?? 0m;
        var realizedPnL = portfolio.RealizedPnL ?? 0m;

        return new PerformanceMetrics(
            TotalReturn: totalReturn,
            UnrealizedPnL: unrealizedPnL,
            RealizedPnL: realizedPnL,
            TotalPnL: unrealizedPnL + realizedPnL,
            Timestamp: DateTimeOffset.UtcNow
        );
    }

    /// <summary>
    /// Get strategy state from StockSharp strategy
    /// </summary>
    /// <param name="strategy">Strategy instance</param>
    /// <returns>Strategy state</returns>
    public static StrategyStatus GetEnhancedStatus(this Strategy strategy)
    {
        if (strategy == null)
            throw new ArgumentNullException(nameof(strategy));

        return strategy.ProcessState switch
        {
            StockSharp.Algo.ProcessStates.Stopped => StrategyStatus.Stopped,
            StockSharp.Algo.ProcessStates.Stopping => StrategyStatus.Stopping,
            StockSharp.Algo.ProcessStates.Started => StrategyStatus.Running,
            _ => StrategyStatus.NotStarted
        };
    }

    #endregion

    #region Logging Extensions

    /// <summary>
    /// Log strategy event with structured logging
    /// </summary>
    /// <param name="strategy">Strategy instance</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="logLevel">Log level</param>
    /// <param name="message">Log message</param>
    /// <param name="args">Message arguments</param>
    public static void LogStrategyEvent(
        this Strategy strategy,
        ILogger logger,
        LogLevel logLevel,
        string message,
        params object[] args)
    {
        if (strategy == null || logger == null || string.IsNullOrEmpty(message))
            return;

        var enrichedArgs = new object[args.Length + 1];
        enrichedArgs[0] = strategy.Name ?? "Unknown";
        Array.Copy(args, 0, enrichedArgs, 1, args.Length);

        logger.Log(logLevel, "[Strategy: {StrategyName}] " + message, enrichedArgs);
    }

    /// <summary>
    /// Log trade execution with enhanced details
    /// </summary>
    /// <param name="strategy">Strategy instance</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="trade">Trade details</param>
    public static void LogTradeExecution(this Strategy strategy, ILogger logger, Trade trade)
    {
        if (strategy == null || logger == null || trade == null)
            return;

        logger.LogInformation(
            "[Strategy: {StrategyName}] Trade executed: {TradeId} {SecurityCode} {Side} {Volume} @ {Price}",
            strategy.Name,
            trade.Id,
            trade.Security?.Code,
            trade.OrderDirection,
            trade.Volume,
            trade.Price);
    }

    #endregion
}

/// <summary>
/// Portfolio metrics record
/// </summary>
/// <param name="TotalPositions">Total number of positions</param>
/// <param name="TotalValue">Total portfolio value</param>
/// <param name="Leverage">Current leverage</param>
/// <param name="Timestamp">Metrics timestamp</param>
public readonly record struct PortfolioMetrics(
    int TotalPositions,
    decimal TotalValue,
    decimal Leverage,
    DateTimeOffset Timestamp
);

/// <summary>
/// Performance metrics record
/// </summary>
/// <param name="TotalReturn">Total return percentage</param>
/// <param name="UnrealizedPnL">Unrealized profit/loss</param>
/// <param name="RealizedPnL">Realized profit/loss</param>
/// <param name="TotalPnL">Total profit/loss</param>
/// <param name="Timestamp">Metrics timestamp</param>
public readonly record struct PerformanceMetrics(
    decimal TotalReturn,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal TotalPnL,
    DateTimeOffset Timestamp
)
{
    /// <summary>
    /// Empty performance metrics
    /// </summary>
    public static readonly PerformanceMetrics Empty = new(0m, 0m, 0m, 0m, DateTimeOffset.UtcNow);
}