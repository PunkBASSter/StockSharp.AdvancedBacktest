# StockSharp.AdvancedBacktest.LauncherTemplate - Technical Requirements Document (TRD)

**Version**: 1.0
**Date**: 2025-10-05
**Status**: Ready for Implementation
**Target Framework**: .NET 10
**Project Type**: Console Application

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Component Design Specifications](#2-component-design-specifications)
3. [Configuration Architecture](#3-configuration-architecture)
4. [Data Flow Architecture](#4-data-flow-architecture)
5. [API Contracts and Interfaces](#5-api-contracts-and-interfaces)
6. [Security Considerations](#6-security-considerations)
7. [Performance Specifications](#7-performance-specifications)
8. [Testing Architecture](#8-testing-architecture)
9. [Deployment Model](#9-deployment-model)
10. [Technical Constraints and Architecture Decisions](#10-technical-constraints-and-architecture-decisions)
11. [Implementation Checklist](#11-implementation-checklist)
12. [File Paths Reference](#12-file-paths-reference)

---

## 1. Architecture Overview

### 1.1 System Context

The LauncherTemplate is a reference implementation that demonstrates all capabilities of the StockSharp.AdvancedBacktest library. It provides dual-mode operation:

- **Backtest Mode**: Complete optimization, validation, and reporting workflow
- **Live Trading Mode**: Real-time strategy execution with broker integration

### 1.2 Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    LauncherTemplate (Console App)               │
└─────────────────────────────────────────────────────────────────┘
                            │
                    ┌───────┴───────┐
                    │   Program.cs  │
                    │  CLI Parser   │
                    └───────┬───────┘
                            │
            ┌───────────────┴──────────────┐
            │                              │
    ┌───────▼───────┐            ┌────────▼────────┐
    │ Backtest Mode │            │  Live Trading   │
    │    Runner     │            │     Engine      │
    └───────┬───────┘            └────────┬────────┘
            │                              │
    ┌───────▼────────┐           ┌────────▼────────┐
    │ StockSharp.    │           │  Broker         │
    │ AdvancedBack-  │           │  Connector      │
    │ test Library   │           │  (StockSharp)   │
    └────────────────┘           └─────────────────┘
```

### 1.3 Component Layers (currently not physically separated)

```
┌─────────────────────────────────────────────────────────┐
│  Presentation Layer (Console CLI)                       │
│  - Program.cs                                           │
│  - LauncherOptions.cs                                   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  Application Layer (Mode Runners)                       │
│  - BacktestRunner                                       │
│  - LiveTradingEngine                                    │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  Domain Layer (Strategy)                                │
│  - PreviousWeekRangeBreakoutStrategy                    │
│  - Risk Management Logic                                │
│  - StockSharp.AdvancedBacktest Library                  │
│  - StockSharp Algo|Strategy|Indicator                   │
│  - StockSharp Framework                                 │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  Infrastructure Layer                                   │
│  - BrokerConnector                                      │
│  - ConfigurationLoader                                  │
│  - StrategyExporter                                     │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│  External Dependencies                                  │
│  - Broker APIs (via StockSharp connectors)              │
└─────────────────────────────────────────────────────────┘
```

---

## 2. Component Design Specifications

### 2.1 Project Structure

```
StockSharp.AdvancedBacktest.LauncherTemplate/
│
├── Program.cs                                    # Entry point with mode routing
├── LauncherOptions.cs                            # CLI argument models
├── StockSharp.AdvancedBacktest.LauncherTemplate.csproj
│
├── Strategies/
│   └── PreviousWeekRangeBreakoutStrategy.cs     # Reference strategy implementation
│
├── BacktestMode/
│   ├── BacktestRunner.cs                        # Orchestrates backtest workflow
│   ├── ConfigurationLoader.cs                   # Loads JSON configurations
│   └── StrategyExporter.cs                      # Exports strategy configs to JSON
│
├── LiveMode/
│   ├── LiveTradingEngine.cs                     # Real-time execution orchestrator
│   ├── BrokerConnector.cs                       # Broker connection abstraction
│   ├── SafetyValidator.cs                       # Pre-flight validation checks
│   ├── RiskManager.cs                           # Real-time risk limit enforcement
│   └── PositionMonitor.cs                       # Position tracking and recovery
│
├── Configuration/
│   ├── Models/
│   │   ├── BacktestConfiguration.cs             # Backtest config model
│   │   ├── LiveTradingConfiguration.cs          # Live trading config model
│   │   ├── StrategyParametersConfig.cs          # Exported strategy parameters
│   │   └── RiskLimitsConfig.cs                  # Risk management settings
│   │
│   └── Validation/
│       └── ConfigurationValidator.cs            # Config validation logic
│
├── Utilities/
│   ├── ConsoleLogger.cs                         # Structured console logging
│   ├── MetricsFormatter.cs                      # Format metrics for display
│   └── JsonSerializationHelper.cs               # System.Text.Json utilities
│
└── ConfigFiles/                                  # Template configurations
    ├── backtest-config.json
    ├── live-config.json
    ├── strategy-export-template.json
    └── ConnectorFile.json.example
```

### 2.2 Key Class Specifications

#### 2.2.1 Program.cs - Main Entry Point

**Purpose**: Application entry point with System.CommandLine CLI parsing

**Key Responsibilities**:

- Parse command-line arguments
- Route to appropriate mode (backtest/live)
- Handle top-level error catching
- Provide user-friendly help text

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate;

public class Program
{
    /// <summary>
    /// Application entry point with command-line argument processing
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Exit code (0 = success, non-zero = error)</returns>
    public static async Task<int> Main(string[] args);

    /// <summary>
    /// Builds the System.CommandLine root command with all subcommands
    /// </summary>
    private static RootCommand BuildCommandLineInterface();

    /// <summary>
    /// Executes backtest mode workflow
    /// </summary>
    private static async Task<int> RunBacktestModeAsync(
        FileInfo configFile,
        DirectoryInfo? outputDir,
        bool verbose,
        int? threads);

    /// <summary>
    /// Executes live trading mode workflow
    /// </summary>
    private static async Task<int> RunLiveTradingModeAsync(
        FileInfo strategyFile,
        FileInfo brokerFile,
        bool dryRun,
        bool requireConfirmation,
        DirectoryInfo? logsDir);

    /// <summary>
    /// Prompts user for confirmation before live trading
    /// </summary>
    private static bool PromptForConfirmation(StrategyParametersConfig strategyConfig);
}
```

**Dependencies**:

- System.CommandLine
- BacktestRunner
- LiveTradingEngine
- ConfigurationLoader

#### 2.2.2 Strategies/PreviousWeekRangeBreakoutStrategy.cs

**Purpose**: Demonstrates advanced strategy implementation with all library features

**Key Responsibilities**:

- Calculate weekly high/low range
- Detect breakout signals
- Apply trend filter
- Manage risk with SL/TP
- Size positions dynamically

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.Strategies;

public class PreviousWeekRangeBreakoutStrategy : CustomStrategyBase
{
    // Optimizable Parameters
    public int TrendFilterPeriod { get; set; } = 50;
    public IndicatorType TrendFilterType { get; set; } = IndicatorType.SMA;
    public StopLossMethod StopLossCalculation { get; set; } = StopLossMethod.Percentage;
    public decimal StopLossPercentage { get; set; } = 2.0m;
    public decimal StopLossATRMultiplier { get; set; } = 2.0m;
    public int ATRPeriod { get; set; } = 14;
    public TakeProfitMethod TakeProfitCalculation { get; set; } = TakeProfitMethod.RiskReward;
    public decimal TakeProfitPercentage { get; set; } = 4.0m;
    public decimal TakeProfitATRMultiplier { get; set; } = 3.0m;
    public decimal RiskRewardRatio { get; set; } = 2.0m;
    public PositionSizingMethod SizingMethod { get; set; } = PositionSizingMethod.PercentOfEquity;
    public decimal FixedPositionSize { get; set; } = 0.01m;
    public decimal EquityPercentage { get; set; } = 2.0m;

    // Lifecycle methods
    protected override void OnStarted(DateTimeOffset time);
    private void OnCandleReceived(Subscription subscription, ICandleMessage candle);

    // Trading logic
    private void CalculateWeeklyRange(DateTimeOffset currentTime);
    private void CheckForBreakoutSignal(ICandleMessage candle);
    private void ExecuteBreakoutTrade(Sides side, decimal price);

    // Risk management
    private decimal CalculatePositionSize(decimal price);
    private decimal CalculateStopLoss(Sides side, decimal entryPrice);
    private decimal CalculateTakeProfit(Sides side, decimal entryPrice, decimal stopLoss);
    private void RegisterProtectiveOrders(Order entryOrder, decimal stopLoss, decimal takeProfit);

    // Helper methods
    private bool IsNewWeek(DateTimeOffset time);
    private DateTimeOffset GetPreviousWeekStart(DateTimeOffset currentTime);
}

// Supporting enums
public enum IndicatorType { SMA, EMA }
public enum StopLossMethod { Percentage, ATR }
public enum TakeProfitMethod { Percentage, ATR, RiskReward }
public enum PositionSizingMethod { Fixed, PercentOfEquity, ATRBased }
```

**Dependencies**:

- StockSharp.AdvancedBacktest.Strategies.CustomStrategyBase
- StockSharp.Algo.Indicators (SMA, EMA, ATR)
- StockSharp.Messages

#### 2.2.3 BacktestMode/BacktestRunner.cs

**Purpose**: Orchestrates complete backtest workflow

**Key Responsibilities**:

- Load and validate configuration
- Build parameter combinations
- Execute optimization
- Run walk-forward validation
- Generate reports
- Export strategy configurations

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

public class BacktestRunner
{
    // Properties
    public string OutputDirectory { get; set; }
    public int ParallelThreads { get; set; }
    public bool VerboseLogging { get; set; }

    // Constructor
    public BacktestRunner(BacktestConfiguration config);

    // Main execution
    /// <summary>
    /// Executes the complete backtest pipeline
    /// </summary>
    public async Task RunAsync();

    // Private workflow methods
    private CustomParamsContainer BuildParameterContainer();
    private async Task<WalkForwardResult> RunWalkForwardValidationAsync(
        OptimizerRunner<PreviousWeekRangeBreakoutStrategy> optimizer,
        OptimizationConfig config);
    private async Task GenerateReportsAsync(
        Dictionary<string, OptimizationResult<PreviousWeekRangeBreakoutStrategy>> results,
        OptimizationConfig config,
        WalkForwardResult? wfResult);
    private async Task ExportTopStrategiesAsync(
        Dictionary<string, OptimizationResult<PreviousWeekRangeBreakoutStrategy>> results,
        OptimizationConfig config);

    // Helper methods
    private List<ICommissionRule> BuildCommissionRules();
    private List<Func<PerformanceMetrics, bool>> BuildMetricFilters();
    private TimeSpan ParseTimeSpan(string timeSpanString);
}
```

**Dependencies**:

- StockSharp.AdvancedBacktest.Optimization.OptimizerRunner
- StockSharp.AdvancedBacktest.Validation.WalkForwardValidator
- StockSharp.AdvancedBacktest.Export.ReportBuilder
- ConfigurationLoader
- StrategyExporter

#### 2.2.4 LiveMode/LiveTradingEngine.cs

**Purpose**: Orchestrates live trading execution with safety and monitoring

**Key Responsibilities**:

- Run pre-flight safety checks
- Connect to broker
- Load and configure strategy
- Monitor execution in real-time
- Enforce risk limits
- Handle graceful shutdown

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.LiveMode;

public class LiveTradingEngine
{
    // Properties
    public bool DryRun { get; set; }
    public string LogDirectory { get; set; } = "./logs";

    // Constructor
    public LiveTradingEngine(
        StrategyParametersConfig strategyConfig,
        LiveTradingConfiguration liveConfig);

    // Main execution
    /// <summary>
    /// Executes the live trading workflow
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken);

    // Private workflow methods
    private async Task RunSafetyChecksAsync();
    private async Task InitializeComponentsAsync();
    private void LoadStrategy();
    private async Task MonitorExecutionAsync(CancellationToken cancellationToken);
    private void DisplayCurrentStatus();
    private void DisplayPerformanceReport();
    private async Task ShutdownGracefullyAsync();
}
```

**Dependencies**:

- BrokerConnector
- SafetyValidator
- RiskManager
- PositionMonitor
- PreviousWeekRangeBreakoutStrategy

#### 2.2.5 LiveMode/SafetyValidator.cs

**Purpose**: Pre-flight validation before live trading

**Key Responsibilities**:

- Validate configuration completeness
- Check broker connectivity
- Verify minimum balance
- Validate strategy parameters
- Perform sanity checks

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.LiveMode;

public class SafetyValidator
{
    public SafetyValidator(LiveTradingConfiguration config);

    /// <summary>
    /// Validates strategy configuration and system readiness
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(StrategyParametersConfig strategyConfig);

    // Private validation methods
    private void ValidateConfiguration(StrategyParametersConfig config, ValidationResult result);
    private void ValidateBrokerConfig(ValidationResult result);
    private void ValidateMinimumBalance(StrategyParametersConfig config, ValidationResult result);
    private void ValidateStrategyParameters(StrategyParametersConfig config, ValidationResult result);
    private void CheckMarketHours(StrategyParametersConfig config, ValidationResult result);
    private async Task CheckConnectivityAsync(ValidationResult result);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
```

#### 2.2.6 LiveMode/RiskManager.cs

**Purpose**: Real-time risk limit enforcement

**Key Responsibilities**:

- Track daily P&L
- Monitor drawdown
- Enforce position size limits
- Limit trade frequency
- Circuit breaker logic

**Public Interface**:

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.LiveMode;

public class RiskManager
{
    // Properties
    public bool DryRun { get; set; }

    // Constructor
    public RiskManager(RiskLimitsConfig limits);

    // Public methods
    /// <summary>
    /// Checks if trading is allowed based on risk limits
    /// </summary>
    public bool CanTrade(PreviousWeekRangeBreakoutStrategy strategy);

    /// <summary>
    /// Called on each trade execution
    /// </summary>
    public void OnTrade(MyTrade trade, PreviousWeekRangeBreakoutStrategy strategy);

    /// <summary>
    /// Validates position size before order submission
    /// </summary>
    public decimal ValidatePositionSize(decimal requestedSize);

    // Private check methods
    private void ResetDailyCountersIfNeeded();
    private bool CheckDailyLossLimit(PreviousWeekRangeBreakoutStrategy strategy);
    private bool CheckMaxDrawdown(PreviousWeekRangeBreakoutStrategy strategy);
    private bool CheckTradeFrequencyLimit();
}
```

---

## 3. Configuration Architecture

### 3.1 Configuration Models Hierarchy

```
BacktestConfiguration
├── Strategy Info
├── Optimization Settings
│   ├── Training Period
│   ├── Validation Period
│   └── Commission Config
├── Parameters Config
│   ├── Securities[]
│   ├── Optimizable Parameters{}
│   └── Fixed Parameters{}
├── Metric Filters Config
├── Walk-Forward Settings
└── Export Settings

LiveTradingConfiguration
├── Strategy Config Path
├── Broker Config Path
├── Risk Management
│   ├── Max Position Size
│   ├── Max Daily Loss
│   ├── Max Drawdown %
│   └── Max Trades Per Day
├── Safety Config
│   ├── Require Confirmation
│   ├── Dry Run
│   └── Minimum Balance
└── Monitoring Config
    ├── Log Settings
    ├── Metrics Update Interval
    └── Performance Report Interval

StrategyParametersConfig (exported)
├── Strategy Name, Version, Hash
├── Optimization Date
├── Parameters
│   ├── Security, Timeframe
│   ├── Trend Filter Settings
│   ├── Risk Management Settings
│   └── Position Sizing Settings
├── Metrics Snapshot
│   ├── Training Metrics
│   ├── Validation Metrics
│   └── Walk-Forward Metrics
└── Trading Settings
```

### 3.2 JSON Serialization Standards

**Serializer**: System.Text.Json (NOT Newtonsoft.Json)

**Options**:

```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters =
    {
        new JsonStringEnumConverter(),
        new DecimalStringConverter() // For precision
    }
};
```

**Decimal Precision Converter**:

```csharp
public class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
}
```

---

## 4. Data Flow Architecture

### 4.1 Backtest Mode Data Flow

```
User Input
    │
    ├─> backtest-config.json
    │
    ▼
ConfigurationLoader
    │
    ├─> Deserialize & Validate
    │
    ▼
BacktestRunner
    │
    ├─> BuildParameterContainer()
    │   └─> Security + Numeric + Class Parameters
    │
    ├─> CreateOptimizationConfig()
    │   └─> Training/Validation Periods, Filters
    │
    ▼
OptimizerRunner (Library)
    │
    ├─> Generate Combinations (N strategies)
    ├─> Execute in Parallel
    │   │
    │   ├─> Strategy Instance #1 → Metrics
    │   ├─> Strategy Instance #2 → Metrics
    │   └─> Strategy Instance #N → Metrics
    │
    ├─> Apply Metric Filters
    │
    ▼
Optimization Results
    │
    ├─────────────┬─────────────┬─────────────┐
    │             │             │             │
    ▼             ▼             ▼             ▼
WalkForward   Report      Strategy      Console
Validator     Builder     Exporter      Output
    │             │             │             │
    ▼             ▼             ▼             ▼
WF Results   index.html  strategy_1    Metrics
JSON         chartData   .json         Summary
             .json       ...
```

### 4.2 Live Trading Mode Data Flow

```
User Input
    │
    ├─> strategy-config.json
    ├─> ConnectorFile.json
    │
    ▼
ConfigurationLoader
    │
    ├─> Load Strategy Config
    ├─> Load Broker Config
    │
    ▼
LiveTradingEngine
    │
    ├─> SafetyValidator
    │   └─> Pre-flight Checks
    │
    ├─> BrokerConnector
    │   └─> Connect to Broker API
    │
    ├─> Load Strategy Instance
    │   └─> Apply Parameters
    │
    ├─> RiskManager
    │   └─> Initialize Limits
    │
    ▼
Strategy.Start()
    │
    ├─────────────┬─────────────┐
    │             │             │
    ▼             ▼             ▼
Market Data   Risk Checks   Position
Stream        (Real-time)   Monitor
    │             │             │
    │             ▼             │
    │         Can Trade?       │
    │         (Yes/No)         │
    │             │             │
    └─────────────┼─────────────┘
                  │
                  ▼
            Order Execution
                  │
                  ├─> Broker API
                  │
                  ▼
            Trade Confirmation
                  │
                  ├─> Update Metrics
                  ├─> Log Trade
                  └─> Update Position
```

### 4.3 Configuration Loading Flow

```
JSON File
    │
    ▼
File.ReadAllTextAsync()
    │
    ▼
JsonSerializer.Deserialize<TConfig>()
    │
    ├─> Parse JSON structure
    ├─> Map to C# model
    ├─> Handle nullables
    │
    ▼
ConfigurationValidator
    │
    ├─> Required fields present?
    ├─> Numeric ranges valid?
    ├─> References exist? (files, etc.)
    ├─> Logical consistency? (SL < TP)
    │
    ├─> Errors found? → Throw exception
    │
    ▼
Configuration Instance (validated)
```

---

## 5. API Contracts and Interfaces

### 5.1 Core Interfaces

#### 5.1.1 IConfigurationLoader

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.Configuration;

/// <summary>
/// Loads and validates configuration files
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Loads backtest configuration from JSON file
    /// </summary>
    Task<BacktestConfiguration> LoadBacktestConfigAsync(string filePath);

    /// <summary>
    /// Loads strategy configuration from JSON file
    /// </summary>
    Task<StrategyParametersConfig> LoadStrategyConfigAsync(string filePath);

    /// <summary>
    /// Loads live trading configuration from JSON file
    /// </summary>
    Task<LiveTradingConfiguration> LoadLiveConfigAsync(string filePath);
}
```

#### 5.1.2 IStrategyExporter

```csharp
namespace StockSharp.AdvancedBacktest.LauncherTemplate.BacktestMode;

/// <summary>
/// Exports strategy configurations to JSON
/// </summary>
public interface IStrategyExporter
{
    /// <summary>
    /// Builds strategy configuration from optimization result
    /// </summary>
    StrategyParametersConfig BuildConfiguration(
        OptimizationResult<PreviousWeekRangeBreakoutStrategy> result,
        OptimizationConfig config,
        string hash);

    /// <summary>
    /// Exports strategy configuration to JSON file
    /// </summary>
    Task ExportAsync(StrategyParametersConfig config, string filePath);
}
```

### 5.2 Public Method Contracts

#### 5.2.1 BacktestRunner.RunAsync()

```csharp
/// <summary>
/// Executes the complete backtest pipeline
/// </summary>
/// <returns>Task representing async operation</returns>
/// <exception cref="InvalidOperationException">Thrown when optimization fails</exception>
/// <exception cref="FileNotFoundException">Thrown when history data not found</exception>
/// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
public async Task RunAsync()
```

**Workflow**:

1. Validate configuration
2. Build parameter container
3. Execute optimization (parallel)
4. Run walk-forward validation (if enabled)
5. Generate HTML reports
6. Export top strategies to JSON
7. Log summary to console

**Outputs**:

- HTML report: `{config.Export.ReportPath}/index.html`
- Strategy configs: `{config.Export.OutputPath}/strategy_*.json`
- Console summary with top metrics

#### 5.2.2 LiveTradingEngine.RunAsync()

```csharp
/// <summary>
/// Executes live trading with monitoring and safety checks
/// </summary>
/// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
/// <returns>Task representing async operation</returns>
/// <exception cref="InvalidOperationException">Thrown when safety checks fail</exception>
/// <exception cref="ConnectionException">Thrown when broker connection fails</exception>
public async Task RunAsync(CancellationToken cancellationToken)
```

**Workflow**:

1. Run pre-flight safety checks
2. Initialize components (broker, risk manager, position monitor)
3. Connect to broker
4. Load strategy with parameters
5. Start strategy execution
6. Monitor with periodic updates
7. Handle cancellation gracefully
8. Close positions and disconnect

**Safety Checks**:

- Configuration completeness
- Broker config file exists
- Minimum balance requirement
- Parameter sanity (SL < TP, etc.)
- Market hours (warnings)

---

## 6. Security Considerations

### 6.1 Credential Management

**Threat**: API keys exposed in ConnectorFile.json

**Mitigations**:

1. **Git Exclusion**:

   ```gitignore
   # .gitignore
   **/ConnectorFile.json
   **/live-config.json
   **/*.credentials.json
   ```

2. **Template Files**:
   - Provide `ConnectorFile.json.example` with placeholder values
   - Users copy and fill real credentials

3. **Environment Variables** (future enhancement):

   ```csharp
   var apiKey = Environment.GetEnvironmentVariable("BROKER_API_KEY")
       ?? config.ApiKey; // Fallback to config
   ```

4. **Encrypted Storage** (future enhancement):
   - Use DPAPI on Windows
   - Use keychain on macOS
   - Use secret manager on Linux

### 6.2 Live Trading Safety

**Threat**: Accidental real-money trading

**Mitigations**:

1. **Explicit Confirmation**:

   ```csharp
   Console.Write("Type 'YES' to confirm live trading: ");
   var input = Console.ReadLine();
   if (input != "YES") return;
   ```

2. **Dry-Run Default**:

   ```bash
   # Safe by default
   dotnet run -- live --strategy config.json --broker broker.json --dry-run

   # Must explicitly disable dry-run
   dotnet run -- live --strategy config.json --broker broker.json --no-dry-run
   ```

3. **Console Warnings**:

   ```
   ╔════════════════════════════════════════╗
   ║  WARNING: LIVE TRADING MODE            ║
   ║  Real money will be used!              ║
   ║  Strategy: WeeklyBreakout              ║
   ║  Capital: $10,000                      ║
   ╚════════════════════════════════════════╝
   ```

4. **Pre-Flight Validation**:
   - All safety checks must pass
   - Errors block execution
   - Warnings require acknowledgment

### 6.3 Input Validation

**Threat**: Invalid configuration causing unexpected behavior

**Mitigations**:

1. **JSON Schema Validation**:
   - Validate structure before deserialization
   - Required fields enforced
   - Type checking

2. **Range Validation**:

   ```csharp
   if (config.StopLossPercentage <= 0 || config.StopLossPercentage >= 100)
       throw new ArgumentException("Stop-loss must be between 0 and 100");
   ```

3. **Logical Validation**:

   ```csharp
   if (config.StopLossPercentage >= config.TakeProfitPercentage)
       warnings.Add("Stop-loss should typically be smaller than take-profit");
   ```

4. **File Existence**:

   ```csharp
   if (!File.Exists(config.HistoryPath))
       throw new FileNotFoundException($"History data not found: {config.HistoryPath}");
   ```

---

## 7. Performance Specifications

### 7.1 Optimization Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **Parameter Combinations** | 500+ | Typical realistic optimization |
| **Optimization Time (500 combos)** | < 10 minutes | On 8-core CPU with SSD |
| **Parallel Efficiency** | > 70% | (Actual time / Sequential time) * cores |
| **Memory Usage** | < 2 GB | Peak RAM during optimization |
| **Disk I/O** | < 100 MB/s | Historical data loading |

**Optimization Algorithm**: Parallel.ForEach with configurable degree of parallelism

**Performance Tuning**:

```csharp
var parallelOptions = new ParallelOptions
{
    MaxDegreeOfParallelism = config.ParallelWorkers > 0
        ? config.ParallelWorkers
        : Environment.ProcessorCount
};
```

### 7.2 Report Generation Performance

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **HTML Generation** | < 5 seconds | Including JSON serialization |
| **Chart Data Size** | < 5 MB | For 1 year of daily candles |
| **Browser Load Time** | < 2 seconds | Opening generated HTML |
| **Chart Rendering** | < 1 second | TradingView chart initialization |

**Optimization Strategies**:

- Compress JSON chart data
- Lazy-load large datasets
- Use efficient JavaScript libraries (TradingView Lightweight Charts)

### 7.3 Live Trading Latency

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| **Order Execution Latency** | < 100 ms | From signal to broker API call |
| **Market Data Processing** | < 50 ms | Per candle update |
| **Risk Check Overhead** | < 10 ms | Per trade evaluation |
| **Position Update** | < 20 ms | After trade confirmation |

**Latency Measurement**:

```csharp
var stopwatch = Stopwatch.StartNew();
_broker.SubmitOrder(order);
stopwatch.Stop();
if (stopwatch.ElapsedMilliseconds > 100)
    logger.LogWarning($"Slow order submission: {stopwatch.ElapsedMilliseconds}ms");
```

---

## 8. Testing Architecture

### 8.1 Test Project Structure

```
StockSharp.AdvancedBacktest.LauncherTemplate.Tests/
│
├── Unit/
│   ├── Configuration/
│   │   ├── ConfigurationLoaderTests.cs
│   │   └── ConfigurationValidatorTests.cs
│   │
│   ├── BacktestMode/
│   │   ├── BacktestRunnerTests.cs
│   │   └── StrategyExporterTests.cs
│   │
│   ├── LiveMode/
│   │   ├── SafetyValidatorTests.cs
│   │   └── RiskManagerTests.cs
│   │
│   └── Strategies/
│       └── PreviousWeekRangeBreakoutStrategyTests.cs
│
├── Integration/
│   ├── BacktestWorkflowTests.cs
│   ├── LiveTradingWorkflowTests.cs
│   └── EndToEndTests.cs
│
├── TestData/
│   ├── Configurations/
│   │   ├── valid-backtest-config.json
│   │   ├── invalid-backtest-config.json
│   │   ├── test-strategy-config.json
│   │   └── test-live-config.json
│   │
│   └── MarketData/
│       ├── sample-btc-daily.json
│       └── sample-eth-daily.json
│
└── Mocks/
    ├── MockBrokerConnector.cs
    ├── MockStrategyExecutor.cs
    └── MockHistoryDataProvider.cs
```

### 8.2 Unit Test Examples

#### 8.2.1 Configuration Loading Tests

```csharp
[Fact]
public async Task ConfigurationLoader_ValidBacktestConfig_LoadsSuccessfully()
{
    // Arrange
    var configPath = "TestData/Configurations/valid-backtest-config.json";
    var loader = new ConfigurationLoader();

    // Act
    var config = await loader.LoadBacktestConfigAsync(configPath);

    // Assert
    Assert.NotNull(config);
    Assert.Equal("backtest", config.Mode);
    Assert.True(config.Parameters.Securities.Count > 0);
    Assert.True(config.Optimization.InitialCapital > 0);
}

[Fact]
public async Task ConfigurationLoader_MissingFile_ThrowsFileNotFoundException()
{
    // Arrange
    var loader = new ConfigurationLoader();

    // Act & Assert
    await Assert.ThrowsAsync<FileNotFoundException>(
        () => loader.LoadBacktestConfigAsync("nonexistent.json")
    );
}

[Fact]
public async Task ConfigurationLoader_InvalidJson_ThrowsJsonException()
{
    // Arrange
    var configPath = "TestData/Configurations/invalid-json.json";
    var loader = new ConfigurationLoader();

    // Act & Assert
    await Assert.ThrowsAsync<JsonException>(
        () => loader.LoadBacktestConfigAsync(configPath)
    );
}
```

#### 8.2.2 Safety Validator Tests

```csharp
[Fact]
public async Task SafetyValidator_ValidConfig_ReturnsValidResult()
{
    // Arrange
    var validator = new SafetyValidator(new LiveTradingConfiguration
    {
        BrokerConfigPath = "TestData/Configurations/valid-broker.json"
    });
    var strategyConfig = CreateValidStrategyConfig();

    // Act
    var result = await validator.ValidateAsync(strategyConfig);

    // Assert
    Assert.True(result.IsValid);
    Assert.Empty(result.Errors);
}

[Fact]
public async Task SafetyValidator_MissingSecurityInConfig_ReturnsError()
{
    // Arrange
    var validator = new SafetyValidator(new LiveTradingConfiguration());
    var config = new StrategyParametersConfig
    {
        Parameters = new StrategyParameters { Security = string.Empty }
    };

    // Act
    var result = await validator.ValidateAsync(config);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Contains("Security"));
}

[Fact]
public async Task SafetyValidator_InvalidStopLossRange_ReturnsError()
{
    // Arrange
    var validator = new SafetyValidator(new LiveTradingConfiguration());
    var config = CreateValidStrategyConfig();
    config.Parameters.StopLossPercentage = 150m; // Invalid: > 100

    // Act
    var result = await validator.ValidateAsync(config);

    // Assert
    Assert.False(result.IsValid);
    Assert.Contains(result.Errors, e => e.Contains("Stop-loss percentage"));
}
```

#### 8.2.3 Risk Manager Tests

```csharp
[Fact]
public void RiskManager_DailyLossExceeded_ReturnsFalse()
{
    // Arrange
    var riskManager = new RiskManager(new RiskLimitsConfig
    {
        MaxDailyLoss = 100m
    });
    var strategy = CreateMockStrategy(portfolioStartValue: 10000m, currentValue: 9800m);

    // Act
    var canTrade = riskManager.CanTrade(strategy);

    // Assert
    Assert.False(canTrade);
}

[Fact]
public void RiskManager_MaxDrawdownExceeded_ReturnsFalse()
{
    // Arrange
    var riskManager = new RiskManager(new RiskLimitsConfig
    {
        MaxDrawdownPercent = 20m
    });
    var strategy = CreateMockStrategy(peakValue: 10000m, currentValue: 7500m); // 25% DD

    // Act
    var canTrade = riskManager.CanTrade(strategy);

    // Assert
    Assert.False(canTrade);
}

[Fact]
public void RiskManager_TradeFrequencyExceeded_ReturnsFalse()
{
    // Arrange
    var riskManager = new RiskManager(new RiskLimitsConfig
    {
        MaxTradesPerDay = 5
    });
    var strategy = CreateMockStrategy();

    // Simulate 5 trades
    for (int i = 0; i < 5; i++)
        riskManager.OnTrade(CreateMockTrade(), strategy);

    // Act
    var canTrade = riskManager.CanTrade(strategy);

    // Assert
    Assert.False(canTrade);
}
```

### 8.3 Integration Test Examples

#### 8.3.1 Backtest Workflow Test

```csharp
[Fact]
public async Task BacktestWorkflow_CompleteRun_GeneratesAllOutputs()
{
    // Arrange
    var config = await ConfigurationLoader.LoadBacktestConfigAsync(
        "TestData/Configurations/test-backtest.json"
    );
    config.Export.OutputPath = Path.Combine(Path.GetTempPath(), "test-strategies");
    config.Export.ReportPath = Path.Combine(Path.GetTempPath(), "test-report");

    var runner = new BacktestRunner(config);

    // Act
    await runner.RunAsync();

    // Assert - Reports generated
    Assert.True(File.Exists(Path.Combine(config.Export.ReportPath, "index.html")));
    Assert.True(File.Exists(Path.Combine(config.Export.ReportPath, "chartData.json")));

    // Assert - Strategies exported
    var strategyFiles = Directory.GetFiles(config.Export.OutputPath, "strategy_*.json");
    Assert.True(strategyFiles.Length >= 1);
    Assert.True(strategyFiles.Length <= config.Export.TopStrategies);

    // Cleanup
    Directory.Delete(config.Export.OutputPath, true);
    Directory.Delete(config.Export.ReportPath, true);
}
```

#### 8.3.2 Live Trading Mock Test

```csharp
[Fact]
public async Task LiveTradingWorkflow_DryRun_ExecutesWithoutRealOrders()
{
    // Arrange
    var strategyConfig = CreateValidStrategyConfig();
    var liveConfig = new LiveTradingConfiguration
    {
        Safety = new SafetyConfig { DryRun = true }
    };

    var mockBroker = new MockBrokerConnector();
    var engine = new LiveTradingEngine(strategyConfig, liveConfig)
    {
        DryRun = true
    };

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Act
    await engine.RunAsync(cts.Token);

    // Assert
    Assert.True(mockBroker.WasConnected);
    Assert.Empty(mockBroker.RealOrdersSubmitted); // No real orders
    Assert.NotEmpty(mockBroker.DryRunOrdersSimulated); // Simulated orders
}
```

### 8.4 Mock Objects

#### 8.4.1 MockBrokerConnector

```csharp
public class MockBrokerConnector : IBrokerConnector
{
    public bool IsConnected { get; private set; }
    public bool WasConnected { get; private set; }
    public List<Order> RealOrdersSubmitted { get; } = new();
    public List<Order> DryRunOrdersSimulated { get; } = new();

    public Connector StockSharpConnector { get; private set; } = new();

    public async Task ConnectAsync(string configPath)
    {
        await Task.Delay(100); // Simulate connection delay
        IsConnected = true;
        WasConnected = true;
    }

    public void Disconnect()
    {
        IsConnected = false;
    }

    public void SubmitOrder(Order order, bool dryRun = false)
    {
        if (dryRun)
        {
            DryRunOrdersSimulated.Add(order);
            // Simulate immediate fill
            order.State = OrderStates.Done;
        }
        else
        {
            RealOrdersSubmitted.Add(order);
        }
    }
}
```

---

## 9. Deployment Model

### 9.1 Project Configuration (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>

    <!-- Assembly Info -->
    <AssemblyName>LauncherTemplate</AssemblyName>
    <Version>1.0.0</Version>
    <Authors>StockSharp.AdvancedBacktest</Authors>
    <Description>Reference implementation for StockSharp.AdvancedBacktest library</Description>

    <!-- Publishing -->
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <SelfContained>false</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <!-- Project References -->
    <ProjectReference Include="..\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.csproj" />

    <!-- CLI Parsing -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />

    <!-- Configuration -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />

    <!-- Logging -->
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />

    <!-- StockSharp Connectors (as needed) -->
    <!-- <PackageReference Include="StockSharp.Binance" Version="5.0.*" /> -->
  </ItemGroup>

  <ItemGroup>
    <!-- Include config templates in output -->
    <None Update="ConfigFiles\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

### 9.2 Build Commands

#### 9.2.1 Development Build

```bash
# Standard debug build
dotnet build

# Release build
dotnet build -c Release
```

#### 9.2.2 Deployment Builds

**Windows (Self-Contained)**:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# Output: bin/Release/net10.0/win-x64/publish/LauncherTemplate.exe (~50-60 MB)
```

**Windows (Framework-Dependent)**:

```bash
dotnet publish -c Release -r win-x64 --self-contained false
# Output: bin/Release/net10.0/win-x64/publish/LauncherTemplate.exe (~1-2 MB)
# Requires .NET 10 Runtime installed
```

**Linux**:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true
# Output: bin/Release/net10.0/linux-x64/publish/LauncherTemplate (~50-60 MB)
```

**macOS**:

```bash
dotnet publish -c Release -r osx-x64 --self-contained true
# Output: bin/Release/net10.0/osx-x64/publish/LauncherTemplate (~50-60 MB)
```

### 9.3 Distribution Package Structure

```
StockSharp.AdvancedBacktest.LauncherTemplate-v1.0.0/
│
├── LauncherTemplate.exe                 # Main executable
│
├── ConfigFiles/
│   ├── backtest-config.json             # Template: Backtest configuration
│   ├── live-config.json                 # Template: Live trading configuration
│   ├── strategy-export-template.json    # Template: Exported strategy format
│   └── ConnectorFile.json.example       # Template: Broker connection (placeholder)
│
├── Examples/
│   ├── example-backtest.json            # Working backtest example
│   ├── example-live.json                # Working live trading example
│   └── README-Examples.md               # How to use examples
│
├── README.md                            # Quick start guide
├── LICENSE                              # License information
├── CHANGELOG.md                         # Version history
│
└── docs/
    ├── Configuration-Guide.md           # Detailed config documentation
    ├── Strategy-Development.md          # How to create strategies
    └── Troubleshooting.md               # Common issues and solutions
```

### 9.4 Runtime Dependencies

**Required**:

- .NET 10 Runtime (if framework-dependent build)
- StockSharp.AdvancedBacktest library (included via project reference)

**Optional**:

- StockSharp broker connectors (Binance, IB, etc.) - depends on use case
- Historical market data (not included, user-provided)

### 9.5 System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **OS** | Windows 10, Linux (Ubuntu 20.04+), macOS 11+ | Windows 11, Ubuntu 22.04, macOS 13+ |
| **CPU** | 2 cores | 8+ cores for optimization |
| **RAM** | 4 GB | 16 GB for large optimizations |
| **Disk Space** | 500 MB | 10 GB+ for market data |
| **Network** | 1 Mbps | 10 Mbps for live trading |
| **.NET** | .NET 10 Runtime | .NET 10 SDK (for development) |

---

## 10. Technical Constraints and Architecture Decisions

### 10.1 ADR-001: Use System.CommandLine for CLI Parsing

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Need robust command-line argument parsing
- Must support subcommands (backtest, live)
- Should auto-generate help text
- Prefer Microsoft-official libraries

**Decision**: Use System.CommandLine (version 2.0 beta)

**Consequences**:

- ✅ **Positive**: Built-in help generation, strong typing, validation, Microsoft support
- ❌ **Negative**: Beta version may have breaking changes before stable release
- ➖ **Neutral**: Alternative (System.Console.GetOpt) requires manual help text

**Rationale**: System.CommandLine is Microsoft's recommended library for .NET CLI applications, with excellent developer experience despite beta status.

---

### 10.2 ADR-002: System.Text.Json for All Serialization

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Need JSON serialization for configurations and exports
- Project guidelines (CLAUDE.md) prohibit Newtonsoft.Json for new code
- Financial precision critical for trading parameters

**Decision**: Use System.Text.Json exclusively, with custom decimal converter

**Consequences**:

- ✅ **Positive**: Native .NET, better performance, source generation support, follows project standards
- ❌ **Negative**: Requires custom converter for decimal precision (28-29 significant digits)
- ➖ **Neutral**: Less feature-rich than Newtonsoft.Json but sufficient for our needs

**Implementation**:

```csharp
public class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
}
```

---

### 10.3 ADR-003: Single Strategy Per Template

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Template should demonstrate library features with concrete example
- Complexity vs. educational value tradeoff
- Users will customize for their own strategies

**Decision**: Implement only PreviousWeekRangeBreakoutStrategy (not multi-strategy framework)

**Consequences**:

- ✅ **Positive**: Simpler codebase, easier to understand, focused examples, clear learning path
- ❌ **Negative**: Users must modify code for different strategies (not plug-and-play)
- ➖ **Neutral**: Future templates can demonstrate multi-strategy patterns if needed

**Rationale**: Reference implementation should prioritize educational clarity over production generality.

---

### 10.4 ADR-004: Broker Connector Abstraction

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Need to support multiple brokers (Binance, Interactive Brokers, etc.)
- StockSharp already provides broker abstractions
- Don't want to re-implement broker protocols

**Decision**: Create thin BrokerConnector wrapper around StockSharp Connector class

**Consequences**:

- ✅ **Positive**: Easy to swap brokers via ConnectorFile.json, leverage StockSharp ecosystem
- ❌ **Negative**: Dependent on StockSharp connector implementations and quality
- ➖ **Neutral**: No custom broker APIs, limited to StockSharp-supported brokers

**Implementation**:

```csharp
public class BrokerConnector
{
    public Connector StockSharpConnector { get; private set; }

    public async Task ConnectAsync(string connectorFilePath)
    {
        StockSharpConnector = new Connector();
        StockSharpConnector.Load(connectorFilePath);
        StockSharpConnector.Connect();
        await WaitForConnectionAsync();
    }
}
```

---

### 10.5 ADR-005: Synchronous Strategy Code, Async Infrastructure

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Strategy logic is event-driven (StockSharp pattern)
- File I/O and report generation benefit from async
- Mixed async/sync patterns can cause confusion

**Decision**:

- Strategy execution: Synchronous (matches StockSharp)
- Configuration loading: Async
- Report generation: Async
- Export operations: Async

**Consequences**:

- ✅ **Positive**: Matches StockSharp patterns, simpler strategy code, async where it matters
- ❌ **Negative**: Mixed async/sync patterns in codebase
- ➖ **Neutral**: Clear separation between strategy logic and infrastructure

**Rationale**: StockSharp strategies are event-driven and synchronous by design. Forcing async would complicate strategy code without benefit.

---

### 10.6 ADR-006: Console Logging for Simplicity

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Need logging for debugging and monitoring
- Full logging frameworks (Serilog, NLog) add complexity
- Reference implementation should be minimal

**Decision**: Use custom ConsoleLogger utility class (not full logging framework)

**Consequences**:

- ✅ **Positive**: Zero configuration, simple to understand, no extra dependencies, fast
- ❌ **Negative**: Limited log rotation, no structured logging to external systems, basic formatting
- ➖ **Neutral**: Can add Microsoft.Extensions.Logging later if users need it

**Implementation**:

```csharp
public static class ConsoleLogger
{
    public static void LogInfo(string message) =>
        Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");

    public static void LogError(string message, Exception? ex = null) =>
        Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {message}\n{ex}");

    public static void LogSection(string title) =>
        Console.WriteLine($"\n=== {title} ===\n");
}
```

---

### 10.7 ADR-007: No Dependency Injection Container

**Status**: Accepted
**Date**: 2025-10-05

**Context**:

- Need to instantiate components (BrokerConnector, RiskManager, etc.)
- DI containers (Microsoft.Extensions.DependencyInjection) add abstraction
- Single-run console app doesn't need complex DI

**Decision**: Use manual instantiation with constructor injection (no DI container)

**Consequences**:

- ✅ **Positive**: Simpler code, easier to understand for beginners, explicit dependencies
- ❌ **Negative**: More manual wiring, slightly harder to unit test (but still testable)
- ➖ **Neutral**: Constructor injection still used, just no container registration

**Rationale**: Template is educational. DI container adds complexity without clear benefit for a simple console app with short lifecycle.

---

## 11. Implementation Checklist

### Phase 1: Project Setup (Days 1-2)

- [ ] Create LauncherTemplate console project (.csproj with .NET 10 target)
- [ ] Add project reference to StockSharp.AdvancedBacktest library
- [ ] Install NuGet packages:
  - [ ] System.CommandLine (2.0.0-beta4)
  - [ ] Microsoft.Extensions.Configuration (9.0.0)
  - [ ] Microsoft.Extensions.Configuration.Json (9.0.0)
  - [ ] Microsoft.Extensions.Logging.Console (9.0.0)
- [ ] Create directory structure:
  - [ ] Strategies/
  - [ ] BacktestMode/
  - [ ] LiveMode/
  - [ ] Configuration/Models/
  - [ ] Configuration/Validation/
  - [ ] Utilities/
  - [ ] ConfigFiles/
- [ ] Add configuration JSON templates to ConfigFiles/
- [ ] Create .gitignore for sensitive files

### Phase 2: Core Infrastructure (Days 3-5)

- [ ] Implement Program.cs with System.CommandLine
  - [ ] Build root command
  - [ ] Add backtest subcommand
  - [ ] Add live subcommand
  - [ ] Wire up handlers
- [ ] Implement configuration models:
  - [ ] BacktestConfiguration.cs
  - [ ] LiveTradingConfiguration.cs
  - [ ] StrategyParametersConfig.cs
  - [ ] RiskLimitsConfig.cs
- [ ] Implement ConfigurationLoader:
  - [ ] LoadBacktestConfigAsync()
  - [ ] LoadStrategyConfigAsync()
  - [ ] LoadLiveConfigAsync()
  - [ ] Add DecimalStringConverter
- [ ] Implement utilities:
  - [ ] ConsoleLogger.cs
  - [ ] MetricsFormatter.cs
  - [ ] JsonSerializationHelper.cs

### Phase 3: Backtest Mode (Days 6-8)

- [ ] Implement BacktestRunner.cs:
  - [ ] RunAsync() main workflow
  - [ ] BuildParameterContainer()
  - [ ] RunWalkForwardValidationAsync()
  - [ ] GenerateReportsAsync()
  - [ ] ExportTopStrategiesAsync()
  - [ ] Helper methods (BuildCommissionRules, BuildMetricFilters)
- [ ] Implement StrategyExporter.cs:
  - [ ] BuildConfiguration()
  - [ ] ExportAsync()
  - [ ] JSON serialization with System.Text.Json

### Phase 4: Strategy Implementation (Days 9-11)

- [ ] Implement PreviousWeekRangeBreakoutStrategy.cs:
  - [ ] Define all optimizable parameters
  - [ ] OnStarted() lifecycle method
  - [ ] OnCandleReceived() event handler
  - [ ] CalculateWeeklyRange()
  - [ ] CheckForBreakoutSignal()
  - [ ] ExecuteBreakoutTrade()
  - [ ] CalculatePositionSize() (Fixed, PercentOfEquity, ATRBased)
  - [ ] CalculateStopLoss() (Percentage, ATR)
  - [ ] CalculateTakeProfit() (Percentage, ATR, RiskReward)
  - [ ] RegisterProtectiveOrders()
  - [ ] Helper methods (IsNewWeek, GetPreviousWeekStart)
- [ ] Add indicator integration:
  - [ ] SimpleMovingAverage / ExponentialMovingAverage
  - [ ] AverageTrueRange
- [ ] Add XML documentation comments

### Phase 5: Live Trading Mode (Days 12-14)

- [ ] Implement LiveTradingEngine.cs:
  - [ ] RunAsync() main workflow
  - [ ] RunSafetyChecksAsync()
  - [ ] InitializeComponentsAsync()
  - [ ] LoadStrategy()
  - [ ] MonitorExecutionAsync()
  - [ ] DisplayCurrentStatus()
  - [ ] DisplayPerformanceReport()
  - [ ] ShutdownGracefullyAsync()
- [ ] Implement BrokerConnector.cs:
  - [ ] ConnectAsync()
  - [ ] Disconnect()
  - [ ] SubscribeToMarketData()
  - [ ] Wrapper around StockSharp Connector
- [ ] Implement SafetyValidator.cs:
  - [ ] ValidateAsync()
  - [ ] ValidateConfiguration()
  - [ ] ValidateBrokerConfig()
  - [ ] ValidateMinimumBalance()
  - [ ] ValidateStrategyParameters()
  - [ ] CheckMarketHours()
  - [ ] CheckConnectivityAsync()
- [ ] Implement RiskManager.cs:
  - [ ] CanTrade()
  - [ ] OnTrade()
  - [ ] ValidatePositionSize()
  - [ ] CheckDailyLossLimit()
  - [ ] CheckMaxDrawdown()
  - [ ] CheckTradeFrequencyLimit()
- [ ] Implement PositionMonitor.cs:
  - [ ] OnPositionChanged()
  - [ ] Track positions
  - [ ] Log trade history

### Phase 6: Testing (Days 15-17)

- [ ] Create test project (xUnit):
  - [ ] Unit/Configuration/ConfigurationLoaderTests.cs
  - [ ] Unit/Configuration/ConfigurationValidatorTests.cs
  - [ ] Unit/BacktestMode/BacktestRunnerTests.cs
  - [ ] Unit/LiveMode/SafetyValidatorTests.cs
  - [ ] Unit/LiveMode/RiskManagerTests.cs
  - [ ] Unit/Strategies/PreviousWeekRangeBreakoutStrategyTests.cs
  - [ ] Integration/BacktestWorkflowTests.cs
  - [ ] Integration/LiveTradingWorkflowTests.cs
- [ ] Create test data:
  - [ ] TestData/Configurations/*.json
  - [ ] TestData/MarketData/*.json
- [ ] Create mock objects:
  - [ ] MockBrokerConnector.cs
  - [ ] MockStrategyExecutor.cs
- [ ] Achieve >80% code coverage

### Phase 7: Documentation (Days 18-19)

- [ ] Create LauncherTemplate README.md:
  - [ ] Quick start guide
  - [ ] Installation instructions
  - [ ] Usage examples (backtest and live)
  - [ ] Configuration reference
- [ ] Update main README.md:
  - [ ] Replace code snippets with LauncherTemplate references
  - [ ] Add "See LauncherTemplate for complete example" sections
  - [ ] Update all feature demonstrations
- [ ] Create configuration guide:
  - [ ] docs/Configuration-Guide.md
  - [ ] All JSON schema documentation
  - [ ] Parameter explanations
- [ ] Create troubleshooting guide:
  - [ ] docs/Troubleshooting.md
  - [ ] Common errors and solutions
  - [ ] FAQ section
- [ ] Add XML documentation to all public APIs

### Phase 8: Polish and Release (Days 20-21)

- [ ] Performance testing:
  - [ ] Run 500+ parameter optimization
  - [ ] Measure execution time
  - [ ] Profile memory usage
- [ ] Cross-platform testing:
  - [ ] Windows build and test
  - [ ] Linux build and test
  - [ ] macOS build and test (if available)
- [ ] Final code review:
  - [ ] Check all TODOs resolved
  - [ ] Verify XML documentation
  - [ ] Ensure consistent code style
  - [ ] Remove debug code
- [ ] Create release package:
  - [ ] Build self-contained executables
  - [ ] Package with templates and docs
  - [ ] Create CHANGELOG.md
- [ ] Tag release version (v1.0.0)

---

## 12. File Paths Reference

### Project Files

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\StockSharp.AdvancedBacktest.LauncherTemplate.csproj`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Program.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LauncherOptions.cs`

### Strategy

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Strategies\PreviousWeekRangeBreakoutStrategy.cs`

### Backtest Mode

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\BacktestMode\BacktestRunner.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\BacktestMode\ConfigurationLoader.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\BacktestMode\StrategyExporter.cs`

### Live Mode

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LiveMode\LiveTradingEngine.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LiveMode\BrokerConnector.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LiveMode\SafetyValidator.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LiveMode\RiskManager.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\LiveMode\PositionMonitor.cs`

### Configuration

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Configuration\Models\BacktestConfiguration.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Configuration\Models\LiveTradingConfiguration.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Configuration\Models\StrategyParametersConfig.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Configuration\Models\RiskLimitsConfig.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Configuration\Validation\ConfigurationValidator.cs`

### Utilities

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Utilities\ConsoleLogger.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Utilities\MetricsFormatter.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\Utilities\JsonSerializationHelper.cs`

### Configuration Templates

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\ConfigFiles\backtest-config.json`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\ConfigFiles\live-config.json`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\ConfigFiles\strategy-export-template.json`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate\ConfigFiles\ConnectorFile.json.example`

### Documentation

- `C:\repos\trading\StockSharp.AdvancedBacktest\docs\4_TRD_LauncherTemplate.md` (this document)
- `C:\repos\trading\StockSharp.AdvancedBacktest\docs\3_PRD_LauncherTemplate.md`
- `C:\repos\trading\StockSharp.AdvancedBacktest\README.md`

### Tests

- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate.Tests\Unit\Configuration\ConfigurationLoaderTests.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate.Tests\Unit\LiveMode\SafetyValidatorTests.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate.Tests\Unit\LiveMode\RiskManagerTests.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate.Tests\Integration\BacktestWorkflowTests.cs`
- `C:\repos\trading\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.LauncherTemplate.Tests\Integration\LiveTradingWorkflowTests.cs`

---

**Document Version**: 1.0
**Created**: 2025-10-05
**Status**: Ready for Implementation
**Estimated Effort**: 21 working days
**Target Delivery**: 3 weeks

**Next Step**: Begin Phase 1 implementation with backend-implementer subagent
