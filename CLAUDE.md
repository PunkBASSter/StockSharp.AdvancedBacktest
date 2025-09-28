# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Building

- **Main Solution**: `dotnet build StockSharp.AdvancedBacktest.slnx` (from repository root)
- **Individual Project**: `dotnet build StockSharp.AdvancedBacktest/StockSharp.AdvancedBacktest.csproj`
- **Legacy Strategy Launcher**: `dotnet build LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj`
- **All Projects**: Build solution from root directory as mentioned in README

### Testing

- **Run Tests**: `dotnet test StockSharp.AdvancedBacktest.Tests/`
- **Test Framework**: xUnit v3 with Microsoft.NET.Test.Sdk

### Running Applications

- **Strategy Launcher**: `dotnet run --project LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj`

## Project Architecture

This is a .NET 10 solution that extends StockSharp for advanced backtesting capabilities. The architecture consists of:

### Core Components

- **StockSharp.AdvancedBacktest**: Main library targeting .NET 10
- **StockSharp.AdvancedBacktest.Tests**: xUnit v3 test project targeting .NET 10
- **StockSharp.AdvancedBacktest.Web**: Web-based visualization component (directory structure only, to be implemented as a Next.js app, static web page with JSON data source)
- **LegacyCustomization/StrategyLauncher**: Console application for strategy execution (targets .NET 8, imports StockSharp build configurations)

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

### JSON Serialization Standards

**Always use System.Text.Json for new implementations. Newtonsoft.Json is acceptable ONLY for reverse compatibility scenarios.**

- **Primary choice**: System.Text.Json with source generation for optimal performance
- **Financial precision**: Custom decimal converters to prevent precision loss in trading calculations
- **Configuration**: JsonSerializerOptions with CamelCase naming policy and null value handling
- **Reverse compatibility**: Newtonsoft.Json usage is limited to scenarios where legacy systems or external dependencies require it (e.g., StockSharp integration dependencies)
- **Migration approach**: When refactoring existing code, evaluate migrating from Newtonsoft.Json to System.Text.Json unless constrained by external dependencies
