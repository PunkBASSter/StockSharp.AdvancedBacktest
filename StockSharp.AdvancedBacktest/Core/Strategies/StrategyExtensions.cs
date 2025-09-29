using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockSharp.Algo.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;
using System.Collections.Immutable;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

public static class StrategyExtensions
{
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

}

public readonly record struct PortfolioMetrics(
    int TotalPositions,
    decimal TotalValue,
    decimal Leverage,
    DateTimeOffset Timestamp
);
public readonly record struct PerformanceMetrics(
    decimal TotalReturn,
    decimal UnrealizedPnL,
    decimal RealizedPnL,
    decimal TotalPnL,
    DateTimeOffset Timestamp
)
{
    public static readonly PerformanceMetrics Empty = new(0m, 0m, 0m, 0m, DateTimeOffset.UtcNow);
}