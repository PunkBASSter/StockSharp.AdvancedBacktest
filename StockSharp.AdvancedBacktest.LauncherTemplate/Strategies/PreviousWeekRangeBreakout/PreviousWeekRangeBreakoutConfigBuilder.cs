using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Strategies.Modules;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies.PreviousWeekRangeBreakout;

// Type-safe builder for PreviousWeekRangeBreakoutStrategy configuration.
// Provides fluent API for constructing validated strategy parameter sets.
//
// Example usage:
// var paramSet = new PreviousWeekRangeBreakoutConfigBuilder()
//     .WithTrendFilter(IndicatorType.SMA, 20)
//     .WithATRPeriod(14)
//     .WithATRBasedPositionSizing(equityPercent: 2m, atrMultiplier: 2m)
//     .WithPercentageStopLoss(2m)
//     .WithRiskRewardTakeProfit(2m)
//     .Build();
public class PreviousWeekRangeBreakoutConfigBuilder
{
    private readonly List<ICustomParam> _params = new();

    // Configures the trend filter with specified indicator type and period.
    public PreviousWeekRangeBreakoutConfigBuilder WithTrendFilter(
        IndicatorType type,
        int period,
        params IndicatorType[] optimizeTypes)
    {
        var types = optimizeTypes.Length > 0
            ? new[] { type }.Concat(optimizeTypes).ToArray()
            : [type];

        _params.Add(new StructParam<IndicatorType>("TrendFilter.Type", types));
        _params.Add(new NumberParam<int>("TrendFilter.Period", period, optimizeFrom: 5, optimizeTo: 200, optimizeStep: 5));
        return this;
    }

    // Configures the ATR (Average True Range) period.
    public PreviousWeekRangeBreakoutConfigBuilder WithATRPeriod(int period)
    {
        _params.Add(new NumberParam<int>("ATR.Period", period, optimizeFrom: 7, optimizeTo: 28, optimizeStep: 7));
        return this;
    }

    // Configures fixed position sizing.
    public PreviousWeekRangeBreakoutConfigBuilder WithFixedPositionSizing(decimal size)
    {
        _params.Add(new StructParam<PositionSizingMethod>("PositionSizing.Method", [PositionSizingMethod.Fixed]));
        _params.Add(new NumberParam<decimal>("PositionSizing.FixedSize", size, optimizeFrom: 0.01m, optimizeTo: 100m, optimizeStep: 0.1m));
        return this;
    }

    public PreviousWeekRangeBreakoutConfigBuilder WithPercentEquityPositionSizing(decimal equityPercent)
    {
        _params.Add(new StructParam<PositionSizingMethod>("PositionSizing.Method", [PositionSizingMethod.PercentOfEquity]));
        _params.Add(new NumberParam<decimal>("PositionSizing.EquityPercent", equityPercent, optimizeFrom: 0.5m, optimizeTo: 10m, optimizeStep: 0.5m));
        return this;
    }

    public PreviousWeekRangeBreakoutConfigBuilder WithATRBasedPositionSizing(
        decimal equityPercent,
        decimal atrMultiplier)
    {
        _params.Add(new StructParam<PositionSizingMethod>("PositionSizing.Method", [PositionSizingMethod.ATRBased]));
        _params.Add(new NumberParam<decimal>("PositionSizing.EquityPercent", equityPercent, optimizeFrom: 0.5m, optimizeTo: 10m, optimizeStep: 0.5m));
        _params.Add(new NumberParam<decimal>("PositionSizing.ATRMultiplier", atrMultiplier, optimizeFrom: 0.5m, optimizeTo: 5m, optimizeStep: 0.25m));
        return this;
    }

    public PreviousWeekRangeBreakoutConfigBuilder OptimizePositionSizingMethod(
        params PositionSizingMethod[] methods)
    {
        if (methods.Length == 0)
            throw new ArgumentException("Must provide at least one method to optimize", nameof(methods));

        _params.Add(new StructParam<PositionSizingMethod>("PositionSizing.Method", methods));
        return this;
    }

    public PreviousWeekRangeBreakoutConfigBuilder WithPercentageStopLoss(decimal percentage)
    {
        _params.Add(new StructParam<StopLossMethod>("StopLoss.Method", [StopLossMethod.Percentage]));
        _params.Add(new NumberParam<decimal>("StopLoss.Percentage", percentage, optimizeFrom: 0.5m, optimizeTo: 10m, optimizeStep: 0.5m));
        return this;
    }

    // Configures ATR-based stop loss.
    public PreviousWeekRangeBreakoutConfigBuilder WithATRStopLoss(decimal atrMultiplier)
    {
        _params.Add(new StructParam<StopLossMethod>("StopLoss.Method", [StopLossMethod.ATR]));
        _params.Add(new NumberParam<decimal>("StopLoss.ATRMultiplier", atrMultiplier, optimizeFrom: 0.5m, optimizeTo: 5m, optimizeStep: 0.25m));
        return this;
    }

    // Adds optimization for stop loss method.
    public PreviousWeekRangeBreakoutConfigBuilder OptimizeStopLossMethod(
        params StopLossMethod[] methods)
    {
        if (methods.Length == 0)
            throw new ArgumentException("Must provide at least one method to optimize", nameof(methods));

        _params.Add(new StructParam<StopLossMethod>("StopLoss.Method", methods));
        return this;
    }

    // Configures percentage-based take profit.
    public PreviousWeekRangeBreakoutConfigBuilder WithPercentageTakeProfit(decimal percentage)
    {
        _params.Add(new StructParam<TakeProfitMethod>("TakeProfit.Method", [TakeProfitMethod.Percentage]));
        _params.Add(new NumberParam<decimal>("TakeProfit.Percentage", percentage, optimizeFrom: 1m, optimizeTo: 20m, optimizeStep: 1m));
        return this;
    }

    // Configures ATR-based take profit.
    public PreviousWeekRangeBreakoutConfigBuilder WithATRTakeProfit(decimal atrMultiplier)
    {
        _params.Add(new StructParam<TakeProfitMethod>("TakeProfit.Method", [TakeProfitMethod.ATR]));
        _params.Add(new NumberParam<decimal>("TakeProfit.ATRMultiplier", atrMultiplier, optimizeFrom: 1m, optimizeTo: 10m, optimizeStep: 0.5m));
        return this;
    }

    // Configures risk/reward ratio-based take profit.
    public PreviousWeekRangeBreakoutConfigBuilder WithRiskRewardTakeProfit(decimal ratio)
    {
        _params.Add(new StructParam<TakeProfitMethod>("TakeProfit.Method", [TakeProfitMethod.RiskReward]));
        _params.Add(new NumberParam<decimal>("TakeProfit.RiskRewardRatio", ratio, optimizeFrom: 1m, optimizeTo: 5m, optimizeStep: 0.5m));
        return this;
    }

    // Adds optimization for take profit method.
    public PreviousWeekRangeBreakoutConfigBuilder OptimizeTakeProfitMethod(
        params TakeProfitMethod[] methods)
    {
        if (methods.Length == 0)
            throw new ArgumentException("Must provide at least one method to optimize", nameof(methods));

        _params.Add(new StructParam<TakeProfitMethod>("TakeProfit.Method", methods));
        return this;
    }

    // Builds and validates the parameter set.
    // Returns list of custom parameters ready to use with CustomStrategyBase.Create().
    // Throws InvalidOperationException if configuration is invalid.
    public List<ICustomParam> Build()
    {
        ValidateConfiguration();
        return _params;
    }

    // Validates that all required parameters are present and method-specific parameters are consistent.
    private void ValidateConfiguration()
    {
        var paramIds = _params.Select(p => p.Id).ToHashSet();

        // Ensure required parameters
        if (!paramIds.Contains("TrendFilter.Type"))
            throw new InvalidOperationException("Trend filter configuration is required. Call WithTrendFilter().");

        if (!paramIds.Contains("TrendFilter.Period"))
            throw new InvalidOperationException("Trend filter period is required. Call WithTrendFilter().");

        if (!paramIds.Contains("ATR.Period"))
            throw new InvalidOperationException("ATR period is required. Call WithATRPeriod().");

        if (!paramIds.Contains("PositionSizing.Method"))
            throw new InvalidOperationException("Position sizing configuration is required. Call one of: WithFixedPositionSizing(), WithPercentEquityPositionSizing(), or WithATRBasedPositionSizing().");

        if (!paramIds.Contains("StopLoss.Method"))
            throw new InvalidOperationException("Stop loss configuration is required. Call WithPercentageStopLoss() or WithATRStopLoss().");

        if (!paramIds.Contains("TakeProfit.Method"))
            throw new InvalidOperationException("Take profit configuration is required. Call WithPercentageTakeProfit(), WithATRTakeProfit(), or WithRiskRewardTakeProfit().");

        // Validate method-specific parameters
        ValidateMethodSpecificParams("PositionSizing", paramIds);
        ValidateMethodSpecificParams("StopLoss", paramIds);
        ValidateMethodSpecificParams("TakeProfit", paramIds);
    }

    private void ValidateMethodSpecificParams(string category, HashSet<string> paramIds)
    {
        var methodParam = _params.FirstOrDefault(p => p.Id == $"{category}.Method");
        if (methodParam == null) return;

        switch (category)
        {
            case "PositionSizing":
                var psMethod = (PositionSizingMethod)methodParam.Value;
                if (psMethod == PositionSizingMethod.Fixed && !paramIds.Contains("PositionSizing.FixedSize"))
                    throw new InvalidOperationException("Fixed position sizing requires FixedSize parameter. Call WithFixedPositionSizing().");
                if (psMethod == PositionSizingMethod.PercentOfEquity && !paramIds.Contains("PositionSizing.EquityPercent"))
                    throw new InvalidOperationException("Percent equity sizing requires EquityPercent parameter. Call WithPercentEquityPositionSizing().");
                if (psMethod == PositionSizingMethod.ATRBased && !paramIds.Contains("PositionSizing.ATRMultiplier"))
                    throw new InvalidOperationException("ATR-based sizing requires ATRMultiplier parameter. Call WithATRBasedPositionSizing().");
                break;

            case "StopLoss":
                var slMethod = (StopLossMethod)methodParam.Value;
                if (slMethod == StopLossMethod.Percentage && !paramIds.Contains("StopLoss.Percentage"))
                    throw new InvalidOperationException("Percentage stop loss requires Percentage parameter. Call WithPercentageStopLoss().");
                if (slMethod == StopLossMethod.ATR && !paramIds.Contains("StopLoss.ATRMultiplier"))
                    throw new InvalidOperationException("ATR stop loss requires ATRMultiplier parameter. Call WithATRStopLoss().");
                break;

            case "TakeProfit":
                var tpMethod = (TakeProfitMethod)methodParam.Value;
                if (tpMethod == TakeProfitMethod.Percentage && !paramIds.Contains("TakeProfit.Percentage"))
                    throw new InvalidOperationException("Percentage take profit requires Percentage parameter. Call WithPercentageTakeProfit().");
                if (tpMethod == TakeProfitMethod.ATR && !paramIds.Contains("TakeProfit.ATRMultiplier"))
                    throw new InvalidOperationException("ATR take profit requires ATRMultiplier parameter. Call WithATRTakeProfit().");
                if (tpMethod == TakeProfitMethod.RiskReward && !paramIds.Contains("TakeProfit.RiskRewardRatio"))
                    throw new InvalidOperationException("Risk/reward take profit requires RiskRewardRatio parameter. Call WithRiskRewardTakeProfit().");
                break;
        }
    }
}
