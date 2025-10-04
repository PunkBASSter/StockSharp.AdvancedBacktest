using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ecng.Collections;
using StockSharp.Algo;
using StockSharp.Algo.Storages;
using StockSharp.Algo.Strategies;
using StockSharp.Algo.Strategies.Optimization;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using StockSharp.AdvancedBacktest.Models;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies;
using StockSharp.AdvancedBacktest.Utilities;
using StockSharp.AdvancedBacktest.Statistics;

namespace StockSharp.AdvancedBacktest.Optimization;

public class OptimizerRunner<TStrategy> where TStrategy : CustomStrategyBase, new()
{
    private ManualResetEventSlim _optimizationCompleteEvent = new(false);
    private OptimizationConfig _config = null!;
    private BruteForceOptimizer _optimizer = null!;
    public List<TStrategy> OptimizationStrategies { get; set; } = [];
    public List<PerformanceMetrics> PerformanceMetrics { get; set; } = [];

    public BaseOptimizer CreateOptimizer(OptimizationConfig config)
    {
        _config = config;
        _optimizationCompleteEvent.Reset();
        var portfolio = Portfolio.CreateSimulator();
        portfolio.BeginValue = _config.InitialCapital;
        var securities = GetSecuritiesFromConfig();
        var secProvider = new CollectionSecurityProvider(securities.Keys);
        var pfProvider = new CollectionPortfolioProvider([portfolio]);
        var localMarketDataDrive = new LocalMarketDataDrive(_config.HistoryPath);
        var storageRegistry = new StorageRegistry
        {
            DefaultDrive = localMarketDataDrive,
        };
        _optimizer = new BruteForceOptimizer(secProvider, pfProvider, storageRegistry);
        _optimizer.EmulationSettings.BatchSize = config.ParallelWorkers;
        _optimizer.EmulationSettings.CommissionRules = config.CommissionRules;

        _optimizer.StateChanged += (oldState, newState) =>
        {
            if (newState == ChannelStates.Stopped)
            {
                _optimizationCompleteEvent.Set();
            }
        };

        return _optimizer;
    }

    private IDictionary<Security, IOrderedEnumerable<TimeSpan>> GetSecuritiesFromConfig()
    {
        var secs = _config.ParamsContainer.CustomParams
            .OfType<SecurityParam>()
            .SelectMany(sp => sp.OptimizationRange)
            .ToDictionary(
                ts => ts.Key,
                ts => ts.AsEnumerable().Distinct().OrderBy(t => t),
                new SecurityIdComparer());

        return secs;
    }

    public Dictionary<string, OptimizationResult<TStrategy>> Optimize()
    {
        ValidateHistory();

        var bruteForceParams = GenerateBruteForceParams(_config.ParamsContainer.CustomParams);
        FilterBruteForceParams(bruteForceParams);
        if (bruteForceParams.Count == 0)
        {
            Console.WriteLine("No optimization parameters found or all parameters have empty ranges. Check your strategy configuration.");
            return new Dictionary<string, OptimizationResult<TStrategy>>();
        }

        var optimizationPairs = new List<(Strategy strategy, IStrategyParam[] parameters)>();
        foreach (List<ICustomParam> paramSet in bruteForceParams)
        {
            var strategyInstance = CreateStrategyInstance(paramSet);
            optimizationPairs.Add(strategyInstance);
        }

        if (optimizationPairs.Count == 0)
        {
            Console.WriteLine("No valid strategy combinations found. Check input strategy parameters.");
            return new Dictionary<string, OptimizationResult<TStrategy>>();
        }

        var startTime = DateTimeOffset.UtcNow;
        OptimizationStrategies = optimizationPairs
            .Select(pair => pair.strategy as TStrategy)
            .Where(s => s != null)
            .ToList()!;
        _optimizer.Start(_config.TrainingPeriod.TrainingStartDate.DateTime, _config.TrainingPeriod.TrainingEndDate.DateTime,
                optimizationPairs, optimizationPairs.Count);

        WaitForCompletion(); //TODO handle possible cancellation

        var optimizationResults = OptimizationStrategies.Select(strategy =>
        {
            var trainMetrics = strategy.PerformanceMetrics;
            return new OptimizationResult<TStrategy>
            {
                Config = _config,
                StartTime = startTime,
                TrainedStrategy = strategy,
                TrainingMetrics = trainMetrics
            };
        }).ToDictionary(x => x.TrainedStrategy.Hash);

        optimizationResults = FilterOptimizationResults(optimizationResults);
        var validationPairs = new List<(Strategy strategy, IStrategyParam[] parameters)>();

        validationPairs = optimizationResults.Select(result =>
        {
            return CreateStrategyInstance(result.Value.TrainedStrategy.ParamsBackup);
        }).ToList();

        if (validationPairs.Count == 0)
        {
            Console.WriteLine("No valid strategy combinations found for validation. Check input strategy parameters.");
            return optimizationResults;
        }

        _optimizer.Start(_config.TrainingPeriod.ValidationStartDate.DateTime, _config.TrainingPeriod.ValidationEndDate.DateTime,
                validationPairs, validationPairs.Count);

        WaitForCompletion(); //TODO handle possible cancellation

        //RETURNED VALIDATED STRATEGY IS NULL, CHECK HASHES!
        var validationStrategies = validationPairs
            .Select(pair => pair.strategy as TStrategy)
            .Where(s => s != null)
            .ToDictionary(strategy => strategy!.Hash, strategy => strategy!);

        var validationPerfMetrics = validationPairs
            .Select(pair => pair.strategy as TStrategy)
            .Where(s => s != null)
            .ToDictionary(strategy => strategy!.Hash, strategy => strategy!.PerformanceMetrics);

        foreach (var resStratHash in optimizationResults.Keys)
        {
            optimizationResults[resStratHash].ValidatedStrategy = validationStrategies.GetValueOrDefault(resStratHash);
            optimizationResults[resStratHash].ValidationMetrics = validationPerfMetrics.GetValueOrDefault(resStratHash);
        }
        return optimizationResults;
    }

    public void WaitForCompletion()
    {
        while (true) //!cancellationToken.IsCancellationRequested
        {
            if (_optimizationCompleteEvent.Wait(100))//ct
            {
                Console.WriteLine("All optimization strategies have completed.");
                break;
            }
        }

        _optimizationCompleteEvent.Reset();
        //if (cancellationToken.IsCancellationRequested)
        //{
        //	Console.WriteLine("Optimization cancelled by user.");
        //	_stockSharpOptimizer.Stop();
        //}
    }

    public void Stop()
    {
        _optimizer?.Stop();
    }

    public List<List<ICustomParam>> GenerateBruteForceParams(IEnumerable<ICustomParam> parameters)
    {
        var optimizationRanges = parameters
            .Where(param => param.CanOptimize)
            .Select(optRng => optRng.OptimizationRangeParams.ToList())
            .ToList();

        return CarthesianProduct(optimizationRanges);
    }

    public List<List<T>> CarthesianProduct<T>(List<List<T>> lists)
    {
        if (lists == null || lists.Count == 0)
            return [];

        if (lists.Any(list => list == null || list.Count == 0))
            return [];

        List<List<T>> result = [];
        var indices = new int[lists.Count];

        while (true)
        {
            var current = new List<T>();
            for (var i = 0; i < lists.Count; i++)
                current.Add(lists[i][indices[i]]);

            result.Add(current);

            var index = lists.Count - 1;
            while (index >= 0 && indices[index] == lists[index].Count - 1)
                index--;

            if (index < 0)
                break;

            indices[index]++;
            for (var i = index + 1; i < lists.Count; i++)
                indices[i] = 0;
        }

        return result;
    }

    private void FilterBruteForceParams(List<List<ICustomParam>> bruteForceParams)
    {
        if (_config.ParamsContainer.ValidationRules.Count == 0)
            return;

        var filteredParams = new List<List<ICustomParam>>();
        foreach (var paramSet in bruteForceParams)
        {
            if (_config.ParamsContainer.ValidationRules
                .All(filter => filter(paramSet.ToDictionary(p => p.Id, p => p))))
            {
                filteredParams.Add(paramSet);
            }
        }

        if (filteredParams.Count == 0)
        {
            Console.WriteLine("No parameter combinations passed the filters.");
            return;
        }

        Console.WriteLine($"Filtered down to {filteredParams.Count} valid parameter combinations.");
        bruteForceParams.Clear();
        bruteForceParams.AddRange(filteredParams);
    }

    private (Strategy strategy, IStrategyParam[] parameters) CreateStrategyInstance(List<ICustomParam> paramSet)
    {
        var strategy = CustomStrategyBase.Create<TStrategy>(paramSet);
        strategy.ParamsBackup = paramSet;
        strategy.Portfolio = Portfolio.CreateSimulator();
        strategy.Portfolio.BeginValue = _config.InitialCapital;

        return (strategy, parameters: strategy.Parameters.Values.ToArray());
    }

    protected void ValidateHistory()
    {
        using var dataDrive = new LocalMarketDataDrive(_config.HistoryPath);
        using var tempRegistry = new StorageRegistry { DefaultDrive = dataDrive };
        var securities = GetSecuritiesFromConfig();
        foreach (var security in securities.Keys)
        {
            var lowestTimeFrame = securities[security].FirstOrDefault();
            var securityId = security.Id.ToSecurityId();
            var candleStorage = tempRegistry.GetCandleMessageStorage(
                typeof(TimeFrameCandleMessage),
                securityId,
                lowestTimeFrame,
                format: StorageFormats.Binary);

            var dates = candleStorage.Dates.ToArray();
            if (dates.Length == 0)
            {
                throw new InvalidOperationException($"No data found for security {security.Id} with timeframe {lowestTimeFrame}");
            }
        }
    }

    private Dictionary<string, OptimizationResult<TStrategy>> FilterOptimizationResults(
        Dictionary<string, OptimizationResult<TStrategy>> optimizationResults)
    {
        if (optimizationResults is null || optimizationResults.Count == 0)
            return new Dictionary<string, OptimizationResult<TStrategy>>();

        var filters = _config.MetricFilters;

        if (filters.Count == 0)
            return optimizationResults;

        var initialCount = optimizationResults.Count;
        var filteredResults = optimizationResults
            .Where(r => r.Value.TrainingMetrics != null &&
                filters.All(filter => filter(r.Value.TrainingMetrics)))
            //.Where(r => r.Value.ValidationMetrics is null || filters.All(filter => filter(r.Value.ValidationMetrics)))
            .ToDictionary(r => r.Key, r => r.Value);

        Console.WriteLine($"\nApplied {filters.Count} filters, {filteredResults.Count} of {initialCount} strategies passed.");

        if (filteredResults.Count == 0)
        {
            Console.WriteLine("Warning: No strategies passed all filters. Using top 3 by total return.");
            return optimizationResults
                .OrderByDescending(r => r.Value.TrainingMetrics?.TotalReturn ?? 0)
                .Take(3)
                .ToDictionary(r => r.Key, r => r.Value);
        }

        return filteredResults;
    }
}
