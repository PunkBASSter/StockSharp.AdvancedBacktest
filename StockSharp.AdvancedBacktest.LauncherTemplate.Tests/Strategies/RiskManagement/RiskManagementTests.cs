using System;
using System.Reflection;
using StockSharp.Algo.Indicators;
using StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;
using StockSharp.BusinessEntities;
using StockSharp.Messages;
using Xunit;

namespace StockSharp.AdvancedBacktest.LauncherTemplate.Tests.Strategies.RiskManagement;

public class RiskManagementTests
{
    private PreviousWeekRangeBreakoutStrategy CreateStrategy(decimal beginValue = 10000m)
    {
        var strategy = new PreviousWeekRangeBreakoutStrategy();
        var portfolio = Portfolio.CreateSimulator();
        portfolio.BeginValue = beginValue;
        strategy.Portfolio = portfolio;
        
        var security = new Security
        {
            Id = "TEST@TEST",
            Code = "TEST",
            Board = ExchangeBoard.Test
        };
        strategy.Security = security;
        
        return strategy;
    }

    private void InitializeATR(PreviousWeekRangeBreakoutStrategy strategy, decimal atrValue)
    {
        var atr = new AverageTrueRange { Length = strategy.ATRPeriod };
        
        strategy.GetType()
            .GetField("_atr", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(strategy, atr);

        var candle = new TimeFrameCandleMessage
        {
            OpenPrice = 100m,
            HighPrice = 100m + atrValue,
            LowPrice = 100m - atrValue,
            ClosePrice = 102m,
            OpenTime = DateTimeOffset.UtcNow,
            CloseTime = DateTimeOffset.UtcNow.AddHours(1),
            State = CandleStates.Finished,
            TypedArg = TimeSpan.FromDays(1),
            SecurityId = new SecurityId { SecurityCode = "TEST" }
        };

        for (int i = 0; i < strategy.ATRPeriod; i++)
        {
            atr.Process(new CandleIndicatorValue(atr, candle));
        }
    }

    private object InvokePrivateMethod(object obj, string methodName, params object[] parameters)
    {
        #pragma warning disable IL2075
        var method = obj.GetType()
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        #pragma warning restore IL2075
        
        if (method == null)
            throw new InvalidOperationException($"Method {methodName} not found");

        return method.Invoke(obj, parameters)!;
    }

    #region CalculatePositionSize Tests

    [Fact]
    public void CalculatePositionSize_FixedMethod_ReturnsConfiguredSize()
    {
        var strategy = CreateStrategy();
        strategy.SizingMethod = PositionSizingMethod.Fixed;
        strategy.FixedPositionSize = 1.5m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculatePositionSize", 100m);

        Assert.Equal(1.5m, result);
    }

    [Fact]
    public void CalculatePositionSize_PercentOfEquity_CalculatesCorrectly()
    {
        var strategy = CreateStrategy(beginValue: 10000m);
        strategy.SizingMethod = PositionSizingMethod.PercentOfEquity;
        strategy.EquityPercentage = 2.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculatePositionSize", 50m);

        var expectedSize = (10000m * 0.02m) / 50m;
        Assert.Equal(expectedSize, result);
    }

    [Fact]
    public void CalculatePositionSize_ATRBased_WithValidATR_CalculatesCorrectly()
    {
        var strategy = CreateStrategy(beginValue: 10000m);
        strategy.SizingMethod = PositionSizingMethod.ATRBased;
        strategy.EquityPercentage = 2.0m;
        strategy.StopLossATRMultiplier = 2.0m;
        
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculatePositionSize", 100m);

        Assert.True(result > 0);
        Assert.True(result >= 0.01m);
    }

    [Fact]
    public void CalculatePositionSize_WithZeroPrice_ThrowsArgumentException()
    {
        var strategy = CreateStrategy();
        strategy.SizingMethod = PositionSizingMethod.Fixed;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculatePositionSize", 0m));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Price must be greater than zero", exception.InnerException!.Message);
    }

    [Fact]
    public void CalculatePositionSize_WithNegativePrice_ThrowsArgumentException()
    {
        var strategy = CreateStrategy();
        strategy.SizingMethod = PositionSizingMethod.Fixed;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculatePositionSize", -50m));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void CalculatePositionSize_BelowMinimum_ThrowsInvalidOperationException()
    {
        var strategy = CreateStrategy(beginValue: 1m);
        strategy.SizingMethod = PositionSizingMethod.PercentOfEquity;
        strategy.EquityPercentage = 1.0m;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculatePositionSize", 10000m));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("below minimum", exception.InnerException!.Message);
    }

    #endregion

    #region CalculateStopLoss Tests

    [Fact]
    public void CalculateStopLoss_PercentageMethod_LongPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Buy, 100m);

        Assert.Equal(98m, result);
    }

    [Fact]
    public void CalculateStopLoss_PercentageMethod_ShortPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Sell, 100m);

        Assert.Equal(102m, result);
    }

    [Fact]
    public void CalculateStopLoss_ATRMethod_LongPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.ATR;
        strategy.StopLossATRMultiplier = 2.0m;
        
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Buy, 100m);

        Assert.Equal(90m, result);
    }

    [Fact]
    public void CalculateStopLoss_ATRMethod_ShortPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.ATR;
        strategy.StopLossATRMultiplier = 2.0m;
        
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Sell, 100m);

        Assert.Equal(110m, result);
    }

    [Fact]
    public void CalculateStopLoss_WithZeroEntryPrice_ThrowsArgumentException()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.Percentage;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Buy, 0m));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Entry price must be greater than zero", exception.InnerException!.Message);
    }

    [Fact]
    public void CalculateStopLoss_LongPosition_ValidatesStopBelowEntry()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Buy, 100m);

        Assert.True(result < 100m, "Stop-loss for long should be below entry");
    }

    [Fact]
    public void CalculateStopLoss_ShortPosition_ValidatesStopAboveEntry()
    {
        var strategy = CreateStrategy();
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Sell, 100m);

        Assert.True(result > 100m, "Stop-loss for short should be above entry");
    }

    #endregion

    #region CalculateTakeProfit Tests

    [Fact]
    public void CalculateTakeProfit_PercentageMethod_LongPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;
        strategy.TakeProfitPercentage = 4.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, 100m, 95m);

        Assert.Equal(104m, result);
    }

    [Fact]
    public void CalculateTakeProfit_PercentageMethod_ShortPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;
        strategy.TakeProfitPercentage = 4.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Sell, 100m, 105m);

        Assert.Equal(96m, result);
    }

    [Fact]
    public void CalculateTakeProfit_ATRMethod_LongPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.ATR;
        strategy.TakeProfitATRMultiplier = 3.0m;
        
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, 100m, 95m);

        Assert.Equal(115m, result);
    }

    [Fact]
    public void CalculateTakeProfit_ATRMethod_ShortPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.ATR;
        strategy.TakeProfitATRMultiplier = 3.0m;
        
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Sell, 100m, 105m);

        Assert.Equal(85m, result);
    }

    [Fact]
    public void CalculateTakeProfit_RiskRewardMethod_LongPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
        strategy.RiskRewardRatio = 2.0m;

        var entryPrice = 100m;
        var stopLoss = 95m;
        var risk = 5m;
        var expectedTP = 100m + (risk * 2.0m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, entryPrice, stopLoss);

        Assert.Equal(expectedTP, result);
    }

    [Fact]
    public void CalculateTakeProfit_RiskRewardMethod_ShortPosition_CalculatesCorrectly()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
        strategy.RiskRewardRatio = 2.0m;

        var entryPrice = 100m;
        var stopLoss = 105m;
        var risk = 5m;
        var expectedTP = 100m - (risk * 2.0m);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Sell, entryPrice, stopLoss);

        Assert.Equal(expectedTP, result);
    }

    [Fact]
    public void CalculateTakeProfit_WithZeroEntryPrice_ThrowsArgumentException()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculateTakeProfit", Sides.Buy, 0m, 95m));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Entry price must be greater than zero", exception.InnerException!.Message);
    }

    [Fact]
    public void CalculateTakeProfit_WithZeroStopLoss_ThrowsArgumentException()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "CalculateTakeProfit", Sides.Buy, 100m, 0m));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Stop-loss must be greater than zero", exception.InnerException!.Message);
    }

    [Fact]
    public void CalculateTakeProfit_LongPosition_ValidatesTPAboveEntry()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;
        strategy.TakeProfitPercentage = 4.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, 100m, 95m);

        Assert.True(result > 100m, "Take-profit for long should be above entry");
    }

    [Fact]
    public void CalculateTakeProfit_ShortPosition_ValidatesTPBelowEntry()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.Percentage;
        strategy.TakeProfitPercentage = 4.0m;

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Sell, 100m, 105m);

        Assert.True(result < 100m, "Take-profit for short should be below entry");
    }

    [Fact]
    public void CalculateTakeProfit_RiskRewardRatio_MaintainsCorrectRatio()
    {
        var strategy = CreateStrategy();
        strategy.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
        strategy.RiskRewardRatio = 3.0m;

        var entryPrice = 100m;
        var stopLoss = 97m;
        var risk = Math.Abs(entryPrice - stopLoss);

        var result = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, entryPrice, stopLoss);

        var reward = Math.Abs(result - entryPrice);
        var actualRatio = reward / risk;

        Assert.Equal(3.0m, actualRatio);
    }

    #endregion

    #region GetCurrentATRValue Tests

    [Fact]
    public void GetCurrentATRValue_WhenATRFormed_ReturnsValidValue()
    {
        var strategy = CreateStrategy();
        InitializeATR(strategy, 5m);

        var result = (decimal)InvokePrivateMethod(strategy, "GetCurrentATRValue");

        Assert.True(result > 0);
    }

    [Fact]
    public void GetCurrentATRValue_WhenATRNotInitialized_ThrowsInvalidOperationException()
    {
        var strategy = CreateStrategy();
        
        var atrField = strategy.GetType()
            .GetField("_atr", BindingFlags.NonPublic | BindingFlags.Instance)!;
        atrField.SetValue(strategy, null);

        var exception = Assert.Throws<TargetInvocationException>(() =>
            InvokePrivateMethod(strategy, "GetCurrentATRValue"));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("ATR indicator is not initialized", exception.InnerException!.Message);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullRiskCalculation_LongPosition_AllMethodsWorkTogether()
    {
        var strategy = CreateStrategy(beginValue: 10000m);
        strategy.SizingMethod = PositionSizingMethod.PercentOfEquity;
        strategy.EquityPercentage = 2.0m;
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;
        strategy.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
        strategy.RiskRewardRatio = 2.0m;

        var entryPrice = 100m;
        var positionSize = (decimal)InvokePrivateMethod(strategy, "CalculatePositionSize", entryPrice);
        var stopLoss = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Buy, entryPrice);
        var takeProfit = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Buy, entryPrice, stopLoss);

        Assert.True(positionSize > 0);
        Assert.True(stopLoss < entryPrice);
        Assert.True(takeProfit > entryPrice);
        Assert.True((takeProfit - entryPrice) / (entryPrice - stopLoss) == 2.0m);
    }

    [Fact]
    public void FullRiskCalculation_ShortPosition_AllMethodsWorkTogether()
    {
        var strategy = CreateStrategy(beginValue: 10000m);
        strategy.SizingMethod = PositionSizingMethod.PercentOfEquity;
        strategy.EquityPercentage = 2.0m;
        strategy.StopLossMethodValue = StopLossMethod.Percentage;
        strategy.StopLossPercentage = 2.0m;
        strategy.TakeProfitMethodValue = TakeProfitMethod.RiskReward;
        strategy.RiskRewardRatio = 2.0m;

        var entryPrice = 100m;
        var positionSize = (decimal)InvokePrivateMethod(strategy, "CalculatePositionSize", entryPrice);
        var stopLoss = (decimal)InvokePrivateMethod(strategy, "CalculateStopLoss", Sides.Sell, entryPrice);
        var takeProfit = (decimal)InvokePrivateMethod(strategy, "CalculateTakeProfit", 
            Sides.Sell, entryPrice, stopLoss);

        Assert.True(positionSize > 0);
        Assert.True(stopLoss > entryPrice);
        Assert.True(takeProfit < entryPrice);
        Assert.True((entryPrice - takeProfit) / (stopLoss - entryPrice) == 2.0m);
    }

    #endregion
}
