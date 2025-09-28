using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

/// <summary>
/// Extension methods for configuring enhanced strategy services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add enhanced strategy services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEnhancedStrategies(this IServiceCollection services)
    {
        return services.AddEnhancedStrategies(options => { });
    }

    /// <summary>
    /// Add enhanced strategy services to the service collection with configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEnhancedStrategies(
        this IServiceCollection services,
        Action<EnhancedStrategyOptions> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Configure options
        var options = new EnhancedStrategyOptions();
        configure(options);
        services.AddSingleton(options);

        // Core services
        services.TryAddSingleton<IStrategyEventHandler, StrategyEventHandler>();
        services.TryAddTransient<IParameterValidator, ParameterValidator>();

        // Performance tracking
        if (options.EnablePerformanceTracking)
        {
            services.TryAddTransient<IPerformanceTracker, PerformanceTracker>();
        }

        // Risk management
        if (options.EnableRiskManagement)
        {
            services.TryAddTransient<IRiskManager, RiskManager>();
        }

        // Object pooling would need custom pool for record types
        // services.AddObjectPool<PerformanceSnapshot>(); // PerformanceSnapshot is a record, can't be pooled this way

        // Logging would be added by the hosting application
        // services.AddLogging();

        return services;
    }

    /// <summary>
    /// Add object pool for a specific type
    /// </summary>
    private static IServiceCollection AddObjectPool<T>(this IServiceCollection services)
        where T : class, new()
    {
        services.TryAddSingleton<ObjectPool<T>>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
            var policy = new DefaultPooledObjectPolicy<T>();
            return provider.Create(policy);
        });

        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();

        return services;
    }

    /// <summary>
    /// Add strategy-specific parameter set
    /// </summary>
    /// <typeparam name="TParameterSet">Parameter set implementation type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddParameterSet<TParameterSet>(this IServiceCollection services)
        where TParameterSet : class, IParameterSet
    {
        services.TryAddTransient<IParameterSet, TParameterSet>();
        return services;
    }

    /// <summary>
    /// Add custom performance tracker implementation
    /// </summary>
    /// <typeparam name="TPerformanceTracker">Performance tracker implementation type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCustomPerformanceTracker<TPerformanceTracker>(this IServiceCollection services)
        where TPerformanceTracker : class, IPerformanceTracker
    {
        services.RemoveAll<IPerformanceTracker>();
        services.AddTransient<IPerformanceTracker, TPerformanceTracker>();
        return services;
    }

    /// <summary>
    /// Add custom risk manager implementation
    /// </summary>
    /// <typeparam name="TRiskManager">Risk manager implementation type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCustomRiskManager<TRiskManager>(this IServiceCollection services)
        where TRiskManager : class, IRiskManager
    {
        services.RemoveAll<IRiskManager>();
        services.AddTransient<IRiskManager, TRiskManager>();
        return services;
    }

    /// <summary>
    /// Add custom event handler implementation
    /// </summary>
    /// <typeparam name="TEventHandler">Event handler implementation type</typeparam>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCustomEventHandler<TEventHandler>(this IServiceCollection services)
        where TEventHandler : class, IStrategyEventHandler
    {
        services.RemoveAll<IStrategyEventHandler>();
        services.AddSingleton<IStrategyEventHandler, TEventHandler>();
        return services;
    }
}

/// <summary>
/// Configuration options for enhanced strategies
/// </summary>
public class EnhancedStrategyOptions
{
    /// <summary>
    /// Whether to enable performance tracking (default: true)
    /// </summary>
    public bool EnablePerformanceTracking { get; set; } = true;

    /// <summary>
    /// Whether to enable risk management (default: true)
    /// </summary>
    public bool EnableRiskManagement { get; set; } = true;

    /// <summary>
    /// Whether to enable high-performance event processing (default: true)
    /// </summary>
    public bool EnableEventProcessing { get; set; } = true;

    /// <summary>
    /// Maximum number of performance snapshots to keep in memory (default: 1000)
    /// </summary>
    public int MaxPerformanceHistory { get; set; } = 1000;

    /// <summary>
    /// Maximum number of risk violations to keep in memory (default: 100)
    /// </summary>
    public int MaxRiskViolationHistory { get; set; } = 100;

    /// <summary>
    /// Default risk management settings
    /// </summary>
    public RiskManagementSettings RiskSettings { get; set; } = new();
}

/// <summary>
/// Default risk management settings
/// </summary>
public class RiskManagementSettings
{
    /// <summary>
    /// Default maximum drawdown limit (default: 10%)
    /// </summary>
    public decimal DefaultMaxDrawdown { get; set; } = 0.10m;

    /// <summary>
    /// Default maximum position size (default: 1000000)
    /// </summary>
    public decimal DefaultMaxPositionSize { get; set; } = 1_000_000m;

    /// <summary>
    /// Default daily loss limit (default: 50000)
    /// </summary>
    public decimal DefaultDailyLossLimit { get; set; } = 50_000m;

    /// <summary>
    /// Whether to enable emergency stop on critical violations (default: true)
    /// </summary>
    public bool EnableEmergencyStop { get; set; } = true;
}