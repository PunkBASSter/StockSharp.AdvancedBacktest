# StockSharp.AdvancedBacktest.LauncherTemplate - Product Requirements Document

## 1. Executive Summary

### 1.1 Overview

StockSharp.AdvancedBacktest.LauncherTemplate is a .NET 10 console application serving as the definitive reference implementation for the StockSharp.AdvancedBacktest library. This project demonstrates ALL library capabilities through a production-ready, dual-mode launcher that supports both backtesting/optimization workflows and live trading execution.

### 1.2 Strategic Objectives

1. **Comprehensive Feature Demonstration**: Showcase every capability of the StockSharp.AdvancedBacktest library through working examples
2. **Production-Ready Template**: Provide a battle-tested foundation for algorithmic trading deployment
3. **Documentation by Example**: Replace README code snippets with references to actual, runnable code
4. **Dual-Mode Architecture**: Seamlessly transition from backtest optimization to live trading execution

### 1.3 Success Criteria

- Successfully demonstrates 100% of library features as documented in README.md
- Implements a profitable sample strategy with realistic trading logic
- Supports one-command execution for both backtest and live trading modes
- Generates comprehensive HTML reports with interactive charts
- Exports production-ready strategy configurations for live deployment
- Serves as the primary code reference for all README.md examples

## 2. Business Objectives

### 2.1 Primary Goals

1. **Educational Excellence**: Provide the most comprehensive example of StockSharp.AdvancedBacktest usage
2. **Rapid Deployment**: Enable traders to go from idea to live trading in hours, not days
3. **Risk Management**: Demonstrate proper validation workflows to prevent overfitting
4. **Production Standards**: Set the quality bar for strategy implementation and deployment

### 2.2 Key Performance Indicators

- Time to first successful optimization: < 5 minutes (including setup)
- Code coverage of library features: 100%
- Number of README examples referencing this project: 100%
- User-reported issues related to unclear documentation: < 5 per quarter

## 3. Target Users and Use Cases

### 3.1 Primary User Personas

#### Persona 1: Quantitative Strategy Developer
- **Background**: Experienced C# developer building algorithmic trading systems
- **Goals**: Learn StockSharp.AdvancedBacktest library by example
- **Pain Points**: Lack of comprehensive, working examples for complex features
- **Expected Outcome**: Clone, run, and modify to create custom strategies

#### Persona 2: Algorithmic Trader
- **Background**: Trading experience with basic programming knowledge
- **Goals**: Deploy validated strategies to live trading
- **Pain Points**: Difficulty transitioning from backtest to production
- **Expected Outcome**: Use template as foundation for live trading system

#### Persona 3: Technical Documentation Reader
- **Background**: Developer reading README.md for first time
- **Goals**: Understand library capabilities through working code
- **Pain Points**: Code snippets don't always compile or run
- **Expected Outcome**: Reference actual files instead of isolated snippets

### 3.2 Core Use Cases

#### Use Case 1: Complete Optimization Workflow
**Actor**: Quantitative Strategy Developer
**Goal**: Run complete backtest → validation → selection → export workflow
**Steps**:
1. Configure strategy parameters and optimization ranges
2. Execute multi-parameter optimization with parallel processing
3. Apply walk-forward validation across time windows
4. Filter results using performance metrics
5. Generate interactive HTML report
6. Export top strategies as JSON configurations

#### Use Case 2: Live Trading Execution
**Actor**: Algorithmic Trader
**Goal**: Deploy optimized strategy to live broker connection
**Steps**:
1. Load strategy configuration from optimization export
2. Connect to live broker (e.g., Binance via ConnectorFile.json)
3. Execute strategy with real-time market data
4. Monitor performance metrics in real-time
5. Apply risk management and position tracking

#### Use Case 3: Learning by Example
**Actor**: New Library User
**Goal**: Understand advanced features through working code
**Steps**:
1. Read README.md feature description
2. Navigate to referenced file in LauncherTemplate
3. Study implementation in context
4. Run example to see results
5. Modify parameters to experiment

## 4. Functional Requirements

### 4.1 Sample Strategy: Previous Week Range Breakout

#### 4.1.1 Strategy Logic

**Trading Rules**:
- **Timeframe**: Daily (D1) candles
- **Setup Period**: Previous trading week (Monday to Friday)
- **Range Calculation**:
  - Weekly High = Highest price of previous week
  - Weekly Low = Lowest price of previous week
- **Entry Signals**:
  - **Long Entry**: Price breaks above Weekly High
  - **Short Entry**: Price breaks below Weekly Low
- **Trend Filter**: 50-period Simple Moving Average (SMA) on D1
  - Only take long trades when price > SMA(50)
  - Only take short trades when price < SMA(50)
- **Risk Management**:
  - **Stop-Loss Options**:
    - Percentage-based: 2% from entry
    - ATR-based: 2x ATR(14)
  - **Take-Profit Options**:
    - Percentage-based: 4% from entry
    - ATR-based: 3x ATR(14)
    - Risk-Reward Ratio: 2:1

#### 4.1.2 Optimizable Parameters

```csharp
// Symbol Selection
SecurityParam: ["BTCUSDT@BNB", "ETHUSDT@BNB", "BNBUSDT@BNB"]
Timeframes: [Daily]

// Trend Filter
TrendFilterPeriod: range(20, 100, step=10)
TrendFilterType: [SMA, EMA]

// Risk Management
StopLossMethod: [Percentage, ATR]
StopLossPercentage: range(1.0, 5.0, step=0.5)
StopLossATRMultiplier: range(1.5, 3.0, step=0.5)
ATRPeriod: [10, 14, 20]

TakeProfitMethod: [Percentage, ATR, RiskReward]
TakeProfitPercentage: range(2.0, 8.0, step=1.0)
TakeProfitATRMultiplier: range(2.0, 4.0, step=0.5)
RiskRewardRatio: [1.5, 2.0, 3.0]

// Position Sizing
PositionSizeMethod: [Fixed, PercentOfEquity, ATRBased]
FixedSize: [0.01, 0.1, 1.0]
EquityPercentage: range(1.0, 5.0, step=1.0)
```

#### 4.1.3 Implementation Structure

```csharp
public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    // Indicators
    private SimpleMovingAverage _trendFilter;
    private AverageTrueRange _atr;

    // Strategy State
    private decimal _weeklyHigh;
    private decimal _weeklyLow;
    private DateTimeOffset _lastWeekEnd;

    // Optimizable Parameters
    public int TrendFilterPeriod { get; set; }
    public string TrendFilterType { get; set; }
    public string StopLossMethod { get; set; }
    public decimal StopLossPercentage { get; set; }
    // ... additional parameters

    protected override void OnStarted(DateTimeOffset time)
    {
        // Initialize indicators
        // Subscribe to candles
        // Calculate initial weekly range
    }

    private void OnCandleReceived(ICandleMessage candle)
    {
        // Update weekly range on new week
        // Check for breakout signals
        // Apply trend filter
        // Execute trades with risk management
    }

    private void CalculateWeeklyRange(DateTimeOffset currentDate)
    {
        // Load previous week's candles
        // Calculate high/low range
    }

    private void ExecuteBreakoutTrade(Sides side, decimal price)
    {
        // Calculate position size
        // Determine stop-loss level
        // Determine take-profit level
        // Place order
    }
}
```

### 4.2 Multi-Parameter Optimization

#### 4.2.1 Optimization Configuration

**Requirements**:
- Support optimization across symbols, timeframes, and strategy parameters
- Generate 500+ parameter combinations for comprehensive testing
- Use parallel processing (all CPU cores) for performance
- Apply parameter validation rules (e.g., StopLoss < TakeProfit)

**Implementation**:
```csharp
var paramsContainer = new CustomParamsContainer
{
    CustomParams = new()
    {
        new SecurityParam("Security", canOptimize: true)
        {
            OptimizationRange = new Dictionary<Security, List<TimeSpan>>
            {
                { new Security { Id = "BTCUSDT@BNB" }, [TimeSpan.FromDays(1)] },
                { new Security { Id = "ETHUSDT@BNB" }, [TimeSpan.FromDays(1)] },
                { new Security { Id = "BNBUSDT@BNB" }, [TimeSpan.FromDays(1)] }
            }
        },
        new NumberParam("TrendFilterPeriod", canOptimize: true,
            start: 20, end: 100, step: 10),
        new ClassParam<IIndicator>("TrendFilterType", canOptimize: true,
            new[] { new SimpleMovingAverage(), new ExponentialMovingAverage() }),
        new NumberParam("StopLossPercentage", canOptimize: true,
            start: 1.0m, end: 5.0m, step: 0.5m)
        // ... additional parameters
    },
    ValidationRules = new()
    {
        (dict) => dict["StopLossPercentage"].Value < dict["TakeProfitPercentage"].Value,
        (dict) => dict["TrendFilterPeriod"].Value >= 10
    }
};

var config = new OptimizationConfig
{
    ParamsContainer = paramsContainer,
    TrainingPeriod = new OptimizationPeriodConfig
    {
        TrainingStartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TrainingEndDate = new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero),
        ValidationStartDate = new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero),
        ValidationEndDate = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)
    },
    HistoryPath = @"C:\MarketData\StockSharp",
    InitialCapital = 10000m,
    TradeVolume = 0.01m,
    ParallelWorkers = Environment.ProcessorCount
};
```

### 4.3 Walk-Forward Validation

#### 4.3.1 Validation Configuration

**Requirements**:
- Implement both anchored and rolling window modes
- Support configurable window sizes (30, 60, 90 days)
- Calculate walk-forward efficiency and consistency metrics
- Detect performance degradation between in-sample and out-of-sample

**Implementation**:
```csharp
var wfConfig = new WalkForwardConfig
{
    WindowSize = TimeSpan.FromDays(90),      // 3-month training window
    StepSize = TimeSpan.FromDays(30),        // Move forward 1 month
    ValidationSize = TimeSpan.FromDays(30),  // 1-month testing period
    Mode = WindowGenerationMode.Anchored     // Expanding window from start
};

var validator = new WalkForwardValidator<PreviousWeekRangeBreakoutStrategy>(
    optimizer, config);

var wfResult = validator.Validate(
    wfConfig,
    startDate: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
    endDate: new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero)
);

Console.WriteLine($"Walk-Forward Efficiency: {wfResult.WalkForwardEfficiency:F2}");
Console.WriteLine($"Consistency Score: {wfResult.Consistency:F2}");
```

### 4.4 Performance Metrics and Filtering

#### 4.4.1 Comprehensive Metrics

**Requirements**:
- Calculate all metrics from PerformanceMetrics class
- Support custom metric filtering for strategy selection
- Display metrics in both console and HTML report

**Metrics Calculated**:
- Returns: Total Return, Annualized Return, Net/Gross Profit
- Risk: Sharpe Ratio, Sortino Ratio, Maximum Drawdown
- Trade Statistics: Win Rate, Profit Factor, Avg Win/Loss
- Activity: Total Trades, Trades per Day

**Implementation**:
```csharp
config.MetricFilters = new()
{
    m => m.TotalReturn > 15.0,        // Min 15% return
    m => m.SharpeRatio > 1.5,         // Min Sharpe ratio
    m => m.SortinoRatio > 2.0,        // Min Sortino ratio
    m => m.MaxDrawdown > -20,         // Max 20% drawdown
    m => m.WinRate > 45,              // Min 45% win rate
    m => m.ProfitFactor > 1.5,        // Min profit factor
    m => m.TotalTrades >= 50,         // Minimum trade count
    m => m.AverageTradesPerDay >= 0.2 // At least 1 trade per week
};
```

### 4.5 Interactive HTML Report Generation

#### 4.5.1 Report Components

**Requirements**:
- Generate self-contained static HTML reports
- Include TradingView charts with candlestick data
- Display trade markers (entry/exit with P&L)
- Show walk-forward analysis results
- Include comprehensive metrics tables

**Implementation**:
```csharp
var bestStrategy = results
    .OrderByDescending(r => r.Value.ValidationMetrics?.SharpeRatio ?? 0)
    .First();

var chartModel = new StrategySecurityChartModel
{
    Strategy = bestStrategy.Value.ValidatedStrategy,
    HistoryPath = config.HistoryPath,
    OutputPath = @"C:\Reports\WeeklyBreakout\index.html",
    WalkForwardResult = wfResult
};

var reportBuilder = new ReportBuilder<PreviousWeekRangeBreakoutStrategy>();
await reportBuilder.GenerateReportAsync(chartModel, @"C:\Reports\WeeklyBreakout");

Console.WriteLine("Report generated: C:\\Reports\\WeeklyBreakout\\index.html");
```

### 4.6 Strategy Configuration Export

#### 4.6.1 JSON Export Format

**Requirements**:
- Export top-N strategies to JSON configuration files
- Include all parameter values and performance metrics
- Structure for easy loading in live trading mode

**JSON Schema**:
```json
{
  "strategyName": "PreviousWeekRangeBreakoutStrategy",
  "version": "1.0.0",
  "hash": "A3F5B8C2",
  "optimizationDate": "2024-12-15T10:30:00Z",
  "parameters": {
    "security": "BTCUSDT@BNB",
    "timeframe": "1.00:00:00",
    "trendFilterPeriod": 50,
    "trendFilterType": "SimpleMovingAverage",
    "stopLossMethod": "Percentage",
    "stopLossPercentage": 2.0,
    "takeProfitMethod": "RiskReward",
    "riskRewardRatio": 2.0,
    "positionSizeMethod": "PercentOfEquity",
    "equityPercentage": 2.0
  },
  "metrics": {
    "training": {
      "totalReturn": 28.5,
      "sharpeRatio": 1.85,
      "sortinoRatio": 2.45,
      "maxDrawdown": -12.3,
      "winRate": 52.5,
      "profitFactor": 1.75,
      "totalTrades": 127
    },
    "validation": {
      "totalReturn": 22.3,
      "sharpeRatio": 1.62,
      "sortinoRatio": 2.18,
      "maxDrawdown": -15.1,
      "winRate": 49.8,
      "profitFactor": 1.58,
      "totalTrades": 68
    },
    "walkForward": {
      "efficiency": 78.2,
      "consistency": 85.5,
      "totalWindows": 10
    }
  },
  "tradingSettings": {
    "initialCapital": 10000.0,
    "tradeVolume": 0.01,
    "commissionPercent": 0.1
  }
}
```

**Implementation**:
```csharp
var topStrategies = results
    .Where(r => r.Value.ValidationMetrics != null)
    .OrderByDescending(r => r.Value.ValidationMetrics.SharpeRatio)
    .Take(5);

foreach (var (index, result) in topStrategies.Select((r, i) => (i + 1, r)))
{
    var exportPath = Path.Combine(
        @"C:\Strategies\Production",
        $"strategy_{index}_{result.Key[..8]}.json"
    );

    var strategyConfig = StrategyConfigExporter.Export(
        result.Value,
        config,
        wfResult
    );

    await File.WriteAllTextAsync(
        exportPath,
        JsonSerializer.Serialize(strategyConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        })
    );

    Console.WriteLine($"Exported: {exportPath}");
}
```

### 4.7 Live Trading Mode

#### 4.7.1 Configuration Loading

**Requirements**:
- Load strategy parameters from exported JSON file
- Validate configuration completeness
- Support hot-reload for parameter updates (optional)

**Implementation**:
```csharp
public class LiveTradingLauncher
{
    public static async Task<PreviousWeekRangeBreakoutStrategy> LoadStrategyAsync(
        string configPath)
    {
        var json = await File.ReadAllTextAsync(configPath);
        var config = JsonSerializer.Deserialize<StrategyConfiguration>(json);

        var strategy = new PreviousWeekRangeBreakoutStrategy
        {
            TrendFilterPeriod = config.Parameters.TrendFilterPeriod,
            StopLossPercentage = config.Parameters.StopLossPercentage,
            // ... load all parameters
        };

        return strategy;
    }
}
```

#### 4.7.2 Broker Connection

**Requirements**:
- Load broker configuration from ConnectorFile.json
- Support multiple brokers (Binance, Interactive Brokers, etc.)
- Handle connection errors and reconnection logic

**ConnectorFile.json Schema** (Based on existing file):
```json
{
  "Adapter": {
    "InnerAdapters": [
      {
        "AdapterType": "StockSharp.Binance.BinanceMessageAdapter, StockSharp.Binance",
        "AdapterSettings": {
          "Id": "unique-adapter-id",
          "Name": "Binance",
          "Key": "encrypted-api-key",
          "Secret": "encrypted-api-secret",
          "IsDemo": false,
          "Sections": "Spot,Futures"
        }
      }
    ]
  },
  "LogLevel": "Info",
  "Name": "LiveConnector"
}
```

**Implementation**:
```csharp
public class BrokerConnector
{
    private Connector _connector;

    public async Task ConnectAsync(string connectorFilePath)
    {
        _connector = new Connector();
        _connector.Load(connectorFilePath);

        _connector.ConnectionError += (ex) =>
            Console.WriteLine($"Connection error: {ex}");

        _connector.Connected += () =>
            Console.WriteLine("Connected to broker");

        _connector.Connect();

        // Wait for connection
        await Task.Delay(2000);
    }

    public void SubscribeToMarketData(Security security, TimeSpan timeframe)
    {
        var subscription = new Subscription(
            DataType.TimeFrame(timeframe),
            security
        );
        _connector.Subscribe(subscription);
    }
}
```

#### 4.7.3 Real-Time Execution

**Requirements**:
- Execute strategy with live market data
- Track positions and orders in real-time
- Apply risk management rules
- Log all trading activity
- Support graceful shutdown

**Implementation**:
```csharp
public class LiveTradingEngine
{
    private readonly PreviousWeekRangeBreakoutStrategy _strategy;
    private readonly BrokerConnector _broker;

    public async Task RunAsync(CancellationToken ct)
    {
        // Attach strategy to connector
        _strategy.Connector = _broker.Connector;
        _strategy.Portfolio = _broker.Connector.Portfolios.First();

        // Start strategy
        _strategy.Start();

        // Monitor performance
        var monitorTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(60000); // Every minute
                Console.WriteLine($"Position: {_strategy.Position}");
                Console.WriteLine($"PnL: {_strategy.PnL}");
            }
        });

        // Wait for cancellation
        await Task.Delay(-1, ct);

        // Graceful shutdown
        _strategy.Stop();
        await monitorTask;
    }
}
```

### 4.8 Dual-Mode Operation

#### 4.8.1 Mode Selection

**Requirements**:
- Support command-line argument for mode selection
- Support configuration file mode specification
- Validate required dependencies for each mode

**Command-Line Interface**:
```bash
# Backtest/Optimization Mode
dotnet run --mode backtest --config backtest-config.json

# Live Trading Mode
dotnet run --mode live --strategy strategy_1_A3F5B8C2.json --broker ConnectorFile.json

# Report Generation Only
dotnet run --mode report --results optimization-results.json --output ./reports
```

**Implementation**:
```csharp
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = ParseArguments(args);

        return options.Mode switch
        {
            "backtest" => await RunBacktestMode(options),
            "live" => await RunLiveTradingMode(options),
            "report" => await RunReportMode(options),
            _ => ShowUsageAndExit()
        };
    }

    private static async Task<int> RunBacktestMode(LauncherOptions options)
    {
        Console.WriteLine("=== BACKTEST MODE ===");

        // 1. Load configuration
        var config = LoadOptimizationConfig(options.ConfigPath);

        // 2. Run optimization
        var optimizer = new OptimizerRunner<PreviousWeekRangeBreakoutStrategy>();
        optimizer.CreateOptimizer(config);
        var results = optimizer.Optimize();

        // 3. Walk-forward validation
        var wfConfig = LoadWalkForwardConfig(options.ConfigPath);
        var validator = new WalkForwardValidator<PreviousWeekRangeBreakoutStrategy>(
            optimizer, config);
        var wfResult = validator.Validate(wfConfig, config.TrainingPeriod.TrainingStartDate,
            config.TrainingPeriod.ValidationEndDate);

        // 4. Generate report
        var reportPath = Path.Combine(options.OutputPath, "report");
        await GenerateReport(results, wfResult, reportPath);

        // 5. Export configurations
        await ExportStrategies(results, Path.Combine(options.OutputPath, "strategies"));

        return 0;
    }

    private static async Task<int> RunLiveTradingMode(LauncherOptions options)
    {
        Console.WriteLine("=== LIVE TRADING MODE ===");

        // 1. Load strategy configuration
        var strategy = await LiveTradingLauncher.LoadStrategyAsync(options.StrategyPath);

        // 2. Connect to broker
        var broker = new BrokerConnector();
        await broker.ConnectAsync(options.BrokerConfigPath);

        // 3. Initialize live trading engine
        var engine = new LiveTradingEngine(strategy, broker);

        // 4. Run with cancellation support
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        await engine.RunAsync(cts.Token);

        return 0;
    }
}
```

### 4.9 Safety Features for Live Trading

#### 4.9.1 Pre-Flight Checks

**Requirements**:
- Validate broker connection before trading
- Verify sufficient account balance
- Check strategy configuration completeness
- Confirm security is tradable

**Implementation**:
```csharp
public class SafetyValidator
{
    public static async Task<ValidationResult> ValidateAsync(
        PreviousWeekRangeBreakoutStrategy strategy,
        BrokerConnector broker)
    {
        var result = new ValidationResult();

        // 1. Broker connection check
        if (!broker.IsConnected)
        {
            result.Errors.Add("Broker not connected");
            return result;
        }

        // 2. Account balance check
        var portfolio = broker.Connector.Portfolios.FirstOrDefault();
        if (portfolio == null || portfolio.CurrentValue < strategy.MinimumCapital)
        {
            result.Errors.Add($"Insufficient balance. Required: {strategy.MinimumCapital}");
            return result;
        }

        // 3. Security validation
        var security = broker.LookupSecurity(strategy.Security.Id);
        if (security == null)
        {
            result.Errors.Add($"Security {strategy.Security.Id} not found");
            return result;
        }

        // 4. Market hours check
        if (!IsMarketOpen(security))
        {
            result.Warnings.Add("Market is currently closed");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}
```

#### 4.9.2 Risk Limits

**Requirements**:
- Maximum position size limits
- Daily loss limits
- Maximum drawdown protection
- Trade frequency limits

**Implementation**:
```csharp
public class RiskManager
{
    public decimal MaxPositionSize { get; set; } = 1.0m;
    public decimal MaxDailyLoss { get; set; } = 500m;
    public decimal MaxDrawdownPercent { get; set; } = 20m;
    public int MaxTradesPerDay { get; set; } = 10;

    private decimal _todayStartBalance;
    private int _todayTradeCount;

    public bool CanTrade(PreviousWeekRangeBreakoutStrategy strategy)
    {
        // Check daily loss limit
        var dailyPnL = strategy.PnL - _todayStartBalance;
        if (dailyPnL < -MaxDailyLoss)
        {
            Console.WriteLine($"Daily loss limit reached: {dailyPnL}");
            return false;
        }

        // Check drawdown
        var drawdown = CalculateDrawdown(strategy);
        if (drawdown > MaxDrawdownPercent)
        {
            Console.WriteLine($"Max drawdown exceeded: {drawdown}%");
            return false;
        }

        // Check trade frequency
        if (_todayTradeCount >= MaxTradesPerDay)
        {
            Console.WriteLine("Max trades per day reached");
            return false;
        }

        return true;
    }

    public decimal ValidatePositionSize(decimal requestedSize)
    {
        return Math.Min(requestedSize, MaxPositionSize);
    }
}
```

## 5. Technical Requirements

### 5.1 Technology Stack

- **Target Framework**: .NET 10
- **Project Type**: Console Application (.exe)
- **Language**: C# 13 with nullable reference types
- **Dependencies**:
  - StockSharp.AdvancedBacktest (project reference)
  - StockSharp.BusinessEntities
  - StockSharp.Algo
  - StockSharp.Binance (or other connectors)
  - System.Text.Json (for configuration)
  - System.CommandLine (for CLI parsing)

### 5.2 Project Structure

```
StockSharp.AdvancedBacktest.LauncherTemplate/
│
├── Program.cs                          # Main entry point with mode selection
├── LauncherOptions.cs                  # CLI argument parsing
│
├── Strategies/
│   └── PreviousWeekRangeBreakoutStrategy.cs  # Sample strategy
│
├── BacktestMode/
│   ├── BacktestRunner.cs               # Orchestrates backtest workflow
│   ├── OptimizationConfigLoader.cs     # Loads optimization config
│   └── StrategyConfigExporter.cs       # Exports strategy to JSON
│
├── LiveMode/
│   ├── LiveTradingLauncher.cs          # Live trading entry point
│   ├── BrokerConnector.cs              # Broker connection management
│   ├── LiveTradingEngine.cs            # Real-time execution engine
│   ├── SafetyValidator.cs              # Pre-flight checks
│   └── RiskManager.cs                  # Real-time risk management
│
├── Configuration/
│   ├── BacktestConfig.cs               # Backtest configuration model
│   ├── StrategyConfiguration.cs        # Strategy config model
│   └── LiveTradingConfig.cs            # Live trading config model
│
├── Utilities/
│   ├── PerformanceLogger.cs            # Console/file logging
│   └── ResultsFormatter.cs             # Format metrics for display
│
└── ConfigFiles/
    ├── backtest-config.json            # Example backtest configuration
    ├── walkforward-config.json         # Walk-forward settings
    ├── ConnectorFile.json              # Broker connection template
    └── strategy-template.json          # Strategy config template
```

### 5.3 Configuration File Formats

#### 5.3.1 Backtest Configuration (backtest-config.json)

```json
{
  "mode": "backtest",
  "strategy": {
    "type": "PreviousWeekRangeBreakoutStrategy",
    "version": "1.0.0"
  },
  "optimization": {
    "historyPath": "C:\\MarketData\\StockSharp",
    "trainingPeriod": {
      "start": "2024-01-01",
      "end": "2024-06-30"
    },
    "validationPeriod": {
      "start": "2024-07-01",
      "end": "2024-12-31"
    },
    "initialCapital": 10000.0,
    "tradeVolume": 0.01,
    "parallelWorkers": -1,
    "commission": {
      "type": "CommissionTradeRule",
      "value": 0.1
    }
  },
  "parameters": {
    "securities": [
      {
        "id": "BTCUSDT@BNB",
        "timeframes": ["1.00:00:00"]
      },
      {
        "id": "ETHUSDT@BNB",
        "timeframes": ["1.00:00:00"]
      }
    ],
    "optimizable": {
      "trendFilterPeriod": {
        "start": 20,
        "end": 100,
        "step": 10
      },
      "stopLossPercentage": {
        "start": 1.0,
        "end": 5.0,
        "step": 0.5
      },
      "takeProfitPercentage": {
        "start": 2.0,
        "end": 8.0,
        "step": 1.0
      }
    },
    "fixed": {
      "atrPeriod": 14
    }
  },
  "metricFilters": {
    "minTotalReturn": 15.0,
    "minSharpeRatio": 1.5,
    "minSortinoRatio": 2.0,
    "maxDrawdown": -20.0,
    "minWinRate": 45.0,
    "minProfitFactor": 1.5,
    "minTotalTrades": 50
  },
  "walkForward": {
    "enabled": true,
    "windowSize": "90.00:00:00",
    "stepSize": "30.00:00:00",
    "validationSize": "30.00:00:00",
    "mode": "Anchored"
  },
  "export": {
    "topStrategies": 5,
    "outputPath": "C:\\Strategies\\Production",
    "reportPath": "C:\\Reports\\WeeklyBreakout"
  }
}
```

#### 5.3.2 Live Trading Configuration (live-config.json)

```json
{
  "mode": "live",
  "strategyConfigPath": "./strategies/strategy_1_A3F5B8C2.json",
  "brokerConfigPath": "./ConnectorFile.json",
  "riskManagement": {
    "maxPositionSize": 1.0,
    "maxDailyLoss": 500.0,
    "maxDrawdownPercent": 20.0,
    "maxTradesPerDay": 10
  },
  "safety": {
    "requireConfirmation": true,
    "dryRun": false,
    "minimumBalance": 1000.0
  },
  "monitoring": {
    "logToFile": true,
    "logPath": "./logs/live-trading.log",
    "metricsUpdateInterval": 60,
    "performanceReportInterval": 3600
  }
}
```

### 5.4 Dependencies and NuGet Packages

```xml
<ItemGroup>
  <!-- StockSharp -->
  <ProjectReference Include="..\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.csproj" />

  <!-- CLI Parsing -->
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />

  <!-- Configuration -->
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />

  <!-- Logging -->
  <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />

  <!-- Additional brokers (as needed) -->
  <!-- <PackageReference Include="StockSharp.Binance" Version="latest" /> -->
  <!-- <PackageReference Include="StockSharp.InteractiveBrokers" Version="latest" /> -->
</ItemGroup>
```

## 6. Example Workflows

### 6.1 Complete Backtest Workflow

```bash
# Step 1: Clone and build
git clone --recurse-submodules https://github.com/YourRepo/StockSharp.AdvancedBacktest.git
cd StockSharp.AdvancedBacktest
dotnet build

# Step 2: Prepare market data (ensure historical data exists)
# Copy data to C:\MarketData\StockSharp or update config

# Step 3: Configure optimization
# Edit ConfigFiles/backtest-config.json with your parameters

# Step 4: Run backtest
cd StockSharp.AdvancedBacktest.LauncherTemplate
dotnet run -- --mode backtest --config ./ConfigFiles/backtest-config.json

# Step 5: Review results
# - Console output shows optimization progress and top strategies
# - HTML report: C:\Reports\WeeklyBreakout\index.html
# - Strategy configs: C:\Strategies\Production\strategy_*.json
```

### 6.2 Live Trading Workflow

```bash
# Step 1: Select validated strategy from backtest
# Choose from C:\Strategies\Production\strategy_*.json

# Step 2: Configure broker connection
# Edit ConnectorFile.json with API credentials

# Step 3: Dry-run test (optional)
dotnet run -- --mode live \
  --strategy ./strategies/strategy_1_A3F5B8C2.json \
  --broker ./ConnectorFile.json \
  --dry-run

# Step 4: Start live trading
dotnet run -- --mode live \
  --strategy ./strategies/strategy_1_A3F5B8C2.json \
  --broker ./ConnectorFile.json

# Step 5: Monitor (Ctrl+C to stop gracefully)
# Logs: ./logs/live-trading.log
# Real-time metrics displayed in console
```

### 6.3 Report-Only Workflow

```bash
# Generate report from existing optimization results
dotnet run -- --mode report \
  --results ./results/optimization-results.json \
  --output ./reports/analysis
```

## 7. Success Criteria

### 7.1 Functional Completeness

- [ ] All README.md features demonstrated in working code
- [ ] Sample strategy implements realistic trading logic
- [ ] Both backtest and live modes fully functional
- [ ] HTML reports generate successfully with charts
- [ ] Strategy export/import works correctly
- [ ] Walk-forward validation produces expected metrics

### 7.2 Code Quality

- [ ] 100% XML documentation on public APIs
- [ ] Follows C# naming conventions
- [ ] Uses System.Text.Json (not Newtonsoft.Json)
- [ ] Proper error handling and logging
- [ ] No hardcoded paths (all configurable)
- [ ] Async/await patterns used correctly

### 7.3 Documentation

- [ ] README.md updated to reference LauncherTemplate files
- [ ] All code snippets in README.md point to actual files
- [ ] Configuration file templates provided with comments
- [ ] Usage examples cover all major scenarios
- [ ] Safety warnings for live trading clearly stated

### 7.4 Performance

- [ ] Optimization completes within reasonable time (< 10 min for 500 combinations)
- [ ] Parallel processing utilizes all CPU cores
- [ ] Live trading latency < 100ms for order execution
- [ ] Memory usage stays within acceptable limits (< 2GB)

## 8. Future Enhancements

### 8.1 Phase 2 Features (Post-MVP)

1. **Multi-Strategy Portfolio**
   - Run multiple strategies simultaneously
   - Portfolio-level risk management
   - Correlation analysis between strategies

2. **Advanced Validation**
   - Monte Carlo simulation for parameter sensitivity
   - Bootstrap analysis for confidence intervals
   - Regime detection and adaptation

3. **Machine Learning Integration**
   - Feature importance analysis
   - Hyperparameter tuning with Bayesian optimization
   - Reinforcement learning for dynamic parameters

4. **Web Dashboard**
   - Real-time monitoring via web UI
   - Remote strategy control
   - Multi-instance management

### 8.2 Platform Extensions

1. **Additional Brokers**
   - Interactive Brokers integration
   - Alpaca integration
   - FTX/Bybit support

2. **Cloud Deployment**
   - Docker containerization
   - Kubernetes orchestration
   - Cloud-native monitoring (Prometheus/Grafana)

3. **Mobile Notifications**
   - Push notifications for trade alerts
   - SMS/Email alerts for critical events
   - Telegram bot integration

## 9. Risk Mitigation

### 9.1 Technical Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Data quality issues | Medium | High | Validate all historical data before optimization |
| Broker API changes | Low | High | Version-lock broker libraries, test before updates |
| Overfitting despite validation | Medium | Critical | Multi-stage validation, conservative metric filters |
| Live trading execution errors | Low | Critical | Extensive pre-flight checks, dry-run mode |

### 9.2 Operational Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| User misconfiguration | High | Medium | Validation on config load, sensible defaults |
| Insufficient testing before live | High | Critical | Mandatory dry-run mode, clear documentation |
| Loss of internet during trading | Medium | High | Auto-reconnect logic, position recovery |
| Account credentials exposure | Medium | Critical | Encrypted storage, never log credentials |

## 10. Acceptance Criteria

### 10.1 Feature Completeness Checklist

- [ ] **Strategy Implementation**: Previous Week Range Breakout strategy fully functional
- [ ] **Multi-Parameter Optimization**: Generates 500+ combinations successfully
- [ ] **Walk-Forward Validation**: Both anchored and rolling modes work correctly
- [ ] **Performance Metrics**: All 15+ metrics calculated accurately
- [ ] **HTML Report Generation**: Interactive charts display correctly in browser
- [ ] **Strategy Export**: JSON files contain complete configuration
- [ ] **Live Trading Mode**: Successfully connects to broker and executes trades
- [ ] **Risk Management**: All safety checks prevent dangerous operations
- [ ] **Dual-Mode CLI**: Command-line arguments control mode selection
- [ ] **Configuration Files**: All templates provided and documented

### 10.2 Documentation Verification

- [ ] README.md references LauncherTemplate for all examples
- [ ] Every major feature has a corresponding code example
- [ ] Configuration file formats are fully documented
- [ ] Safety warnings are prominent and clear
- [ ] Installation and setup instructions are complete

### 10.3 Quality Assurance

- [ ] No compiler warnings
- [ ] All public APIs have XML documentation
- [ ] Error messages are helpful and actionable
- [ ] Logging provides adequate troubleshooting information
- [ ] Code follows project conventions (see CLAUDE.md)

---

## Appendix A: CLI Usage Reference

```bash
StockSharp.AdvancedBacktest.LauncherTemplate

USAGE:
  LauncherTemplate [options]

OPTIONS:
  --mode <backtest|live|report>     Operating mode (required)
  --config <path>                   Configuration file path
  --strategy <path>                 Strategy config (live mode)
  --broker <path>                   Broker config (live mode)
  --output <path>                   Output directory
  --dry-run                         Simulate without real orders
  --verbose                         Enable detailed logging
  --help                            Show help

EXAMPLES:
  # Run backtest
  LauncherTemplate --mode backtest --config backtest-config.json

  # Start live trading
  LauncherTemplate --mode live --strategy strategy.json --broker broker.json

  # Dry run (paper trading)
  LauncherTemplate --mode live --strategy strategy.json --broker broker.json --dry-run

  # Generate report only
  LauncherTemplate --mode report --config results.json --output ./reports
```

## Appendix B: README.md Integration Plan

Update README.md sections to reference LauncherTemplate:

**Before**:
```markdown
### Creating a Strategy

```csharp
public class MyStrategy : CustomStrategyBase
{
    // ... code snippet
}
```
```

**After**:
```markdown
### Creating a Strategy

See the complete implementation in [PreviousWeekRangeBreakoutStrategy.cs](StockSharp.AdvancedBacktest.LauncherTemplate/Strategies/PreviousWeekRangeBreakoutStrategy.cs):

```csharp
// Key excerpts:
public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    // Full implementation available in repository
}
```

Run the example:
```bash
cd StockSharp.AdvancedBacktest.LauncherTemplate
dotnet run -- --mode backtest --config ConfigFiles/backtest-config.json
```
```

---

**Document Version**: 1.0
**Created**: 2025-10-05
**Status**: Ready for Implementation
**Next Step**: Hand off to system-architect subagent for technical design
