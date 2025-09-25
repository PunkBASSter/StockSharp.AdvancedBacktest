# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building
- **Main Solution**: `dotnet build StockSharp.AdvancedBacktest.slnx`
- **Individual Project**: `dotnet build StockSharp.AdvancedBacktest/StockSharp.AdvancedBacktest.csproj`
- **Legacy Strategy Launcher**: `dotnet build LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj`

### Testing
- **Run Tests**: `dotnet test StockSharp.AdvancedBacktest.Tests/`
- **Test Framework**: xUnit v3 with Microsoft.NET.Test.Sdk

### Running Applications
- **Strategy Launcher**: `dotnet run --project LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj`

## Project Architecture

This is a .NET 10 solution that extends StockSharp for advanced backtesting capabilities. The architecture consists of:

### Core Components
- **StockSharp.AdvancedBacktest**: Main library targeting .NET 10
- **StockSharp.AdvancedBacktest.Tests**: xUnit test project
- **StockSharp.AdvancedBacktest.Web**: Web-based visualization component
- **LegacyCustomization/StrategyLauncher**: Console application for strategy execution (.NET 8)

### StockSharp Integration
The project depends on StockSharp through a symbolic link:
- StockSharp repository should be cloned separately
- Create symlink: `New-Item -ItemType Junction -Path ".\StockSharp" -Target "..\StockSharpFork"`
- Strategy Launcher imports StockSharp build configurations and references StockSharp projects

### Key Features
- Dynamic strategy module optimization across symbols/timeframes
- Continuous validation with metric-based selection (Sharpe ratio, drawdown, etc.)
- JSON export of optimization results for web visualization
- Interactive web reports with candlestick charts, trades, and metrics
- Cross-platform console strategy launcher with JSON settings import

### Framework Versions
- Main projects: .NET 10
- Legacy Strategy Launcher: .NET 8 (for StockSharp compatibility)
- C# features: Implicit usings and nullable reference types enabled

### Testing Setup
- xUnit v3 framework
- Microsoft Testing Platform support (commented out, requires .NET 10 SDK)
- Test runner configuration via `xunit.runner.json`