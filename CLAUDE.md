# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building

- **Main Solution**: `dotnet build StockSharp.AdvancedBacktest.slnx` (from repository root)
- **Core Assembly**: `dotnet build StockSharp.AdvancedBacktest.Core/StockSharp.AdvancedBacktest.Core.csproj`
- **Infrastructure Assembly**: `dotnet build StockSharp.AdvancedBacktest.Infrastructure/StockSharp.AdvancedBacktest.Infrastructure.csproj`
- **Legacy Strategy Launcher**: `dotnet build LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj`
- **All Projects**: Build solution from root directory as mentioned in README

### Testing

- **Run All Tests**: `dotnet test StockSharp.AdvancedBacktest.slnx`
- **Core Tests Only**: `dotnet test StockSharp.AdvancedBacktest.Core.Tests/`
- **Infrastructure Tests Only**: `dotnet test StockSharp.AdvancedBacktest.Infrastructure.Tests/`
- **Integration Tests**: `dotnet test StockSharp.AdvancedBacktest.Tests/`
- **Test Framework**: xUnit v3 with Microsoft.NET.Test.Sdk

### Running Applications

- **Strategy Launcher Template**: `dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate/StockSharp.AdvancedBacktest.LauncherTemplate.csproj`
- **Debug MCP Server**: `dotnet run --project StockSharp.AdvancedBacktest.DebugEventLogMcpServer/StockSharp.AdvancedBacktest.DebugEventLogMcpServer.csproj`

## Project Architecture

This is a .NET 8 solution that extends StockSharp for advanced backtesting capabilities. The architecture follows a **Core/Infrastructure separation pattern** with one-way dependencies.

### Assembly Structure

```
StockSharp.AdvancedBacktest.Core/          # Business logic (no infrastructure dependencies)
├── Strategies/                             # CustomStrategyBase, pluggable modules
│   └── Modules/                           # Position sizing, stop-loss, take-profit
├── OrderManagement/                        # TradeSignal, OrderPositionManager
├── Parameters/                             # ICustomParam, NumberParam, SecurityParam, etc.
├── Statistics/                             # PerformanceMetrics, PerformanceMetricsCalculator
├── PerformanceValidation/                  # WalkForwardConfig, WalkForwardResult (models)
├── Models/                                 # OptimizationConfig, BacktestConfig, etc.
├── Backtest/                              # BacktestConfig, BacktestResult, PeriodConfig
└── Utilities/                             # PriceStepHelper, SecurityIdComparer

StockSharp.AdvancedBacktest.Infrastructure/ # Operational/infrastructure code
├── Export/                                # ReportBuilder, BacktestExporter, IndicatorExporter
├── DebugMode/                             # DebugModeExporter, AiAgenticDebug/
│   └── AiAgenticDebug/                    # EventLogging, McpServer subsystems
├── Optimization/                          # OptimizerRunner, OptimizationLauncher
├── PerformanceValidation/                 # WalkForwardValidator (orchestration)
├── Backtest/                              # BacktestRunner (orchestration)
├── Storages/                              # SharedStorageRegistry, SharedMarketDataStorage
├── Serialization/                         # JSON converters and options
└── Utilities/                             # CartesianProductGenerator, IndicatorValueHelper
```

### Dependency Flow

```
StockSharp (submodule)
       ↑
StockSharp.AdvancedBacktest.Core
       ↑
StockSharp.AdvancedBacktest.Infrastructure
       ↑
Application Projects (LauncherTemplate, DebugEventLogMcpServer)
```

**Rule**: Infrastructure depends on Core. Core MUST NOT depend on Infrastructure.

### Test Projects

- **StockSharp.AdvancedBacktest.Core.Tests**: Unit tests for Core assembly (isolated business logic)
- **StockSharp.AdvancedBacktest.Infrastructure.Tests**: Unit tests for Infrastructure assembly
- **StockSharp.AdvancedBacktest.Tests**: Integration tests spanning both assemblies
- **StockSharp.AdvancedBacktest.DebugEventLogMcpServer.Tests**: MCP server specific tests

### Application Projects

- **StockSharp.AdvancedBacktest.LauncherTemplate**: Console application template for strategy execution
- **StockSharp.AdvancedBacktest.DebugEventLogMcpServer**: Standalone MCP server for debug event logging
- **StockSharp.AdvancedBacktest.Web**: Web-based visualization (Next.js, static JSON data source)
- **LegacyCustomization/StrategyLauncher**: Legacy console launcher (.NET 8, StockSharp compatibility)

### StockSharp Integration

The project depends on StockSharp through a git submodule:

- StockSharp is included as a submodule at `./StockSharp`
- Initialize submodules: `git submodule update --init --recursive`
- When cloning fresh: `git clone --recurse-submodules <repo-url>`
- Strategy Launcher imports StockSharp build configurations and references StockSharp projects

### Key Features

- Dynamic strategy module optimization across symbols/timeframes
- Continuous validation with metric-based selection (Sharpe ratio, drawdown, etc.)
- JSON export of optimization results for web visualization
- Interactive web reports with candlestick charts, trades, and metrics
- Cross-platform console strategy launcher with JSON settings import
- AI-assisted debug mode with MCP server integration

### Framework Versions

- Main projects: .NET 8
- Legacy Strategy Launcher: .NET 8 (for StockSharp compatibility)
- C# features: Implicit usings and nullable reference types enabled

### Testing Setup

- xUnit v3 framework
- Microsoft Testing Platform support
- Test runner configuration via `xunit.runner.json`
- Separate test projects per assembly for isolation

### JSON Serialization Standards

**Always use System.Text.Json for new implementations. Newtonsoft.Json is acceptable ONLY for reverse compatibility scenarios.**

- **Primary choice**: System.Text.Json with source generation for optimal performance
- **Financial precision**: Custom decimal converters to prevent precision loss in trading calculations
- **Configuration**: JsonSerializerOptions with CamelCase naming policy and null value handling
- **Reverse compatibility**: Newtonsoft.Json usage is limited to scenarios where legacy systems or external dependencies require it (e.g., StockSharp integration dependencies)
- **Migration approach**: When refactoring existing code, evaluate migrating from Newtonsoft.Json to System.Text.Json unless constrained by external dependencies
