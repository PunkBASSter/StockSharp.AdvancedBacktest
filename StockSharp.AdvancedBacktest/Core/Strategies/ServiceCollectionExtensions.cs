using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using StockSharp.AdvancedBacktest.Core.Strategies.Interfaces;
using StockSharp.AdvancedBacktest.Core.Strategies.Models;

namespace StockSharp.AdvancedBacktest.Core.Strategies;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEnhancedStrategies(this IServiceCollection services)
    {
        return services.AddEnhancedStrategies(options => { });
    }

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

    public static IServiceCollection AddParameterSet<TParameterSet>(this IServiceCollection services)
        where TParameterSet : class, IParameterSet
    {
        services.TryAddTransient<IParameterSet, TParameterSet>();
        return services;
    }

    public static IServiceCollection AddCustomPerformanceTracker<TPerformanceTracker>(this IServiceCollection services)
        where TPerformanceTracker : class, IPerformanceTracker
    {
        services.RemoveAll<IPerformanceTracker>();
        services.AddTransient<IPerformanceTracker, TPerformanceTracker>();
        return services;
    }

    public static IServiceCollection AddCustomRiskManager<TRiskManager>(this IServiceCollection services)
        where TRiskManager : class, IRiskManager
    {
        services.RemoveAll<IRiskManager>();
        services.AddTransient<IRiskManager, TRiskManager>();
        return services;
    }

    public static IServiceCollection AddCustomEventHandler<TEventHandler>(this IServiceCollection services)
        where TEventHandler : class, IStrategyEventHandler
    {
        services.RemoveAll<IStrategyEventHandler>();
        services.AddSingleton<IStrategyEventHandler, TEventHandler>();
        return services;
    }
}

public class EnhancedStrategyOptions
{
    public bool EnablePerformanceTracking { get; set; } = true;

    public bool EnableRiskManagement { get; set; } = true;

    public bool EnableEventProcessing { get; set; } = true;

    public int MaxPerformanceHistory { get; set; } = 1000;

    public int MaxRiskViolationHistory { get; set; } = 100;

    public RiskManagementSettings RiskSettings { get; set; } = new();
}
public class RiskManagementSettings
{
    public decimal DefaultMaxDrawdown { get; set; } = 0.10m;

    public decimal DefaultMaxPositionSize { get; set; } = 1_000_000m;

    public decimal DefaultDailyLossLimit { get; set; } = 50_000m;

    public bool EnableEmergencyStop { get; set; } = true;
}