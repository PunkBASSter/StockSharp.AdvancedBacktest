# StockSharp.AdvancedBacktest

A .NET 8 library extending StockSharp for advanced backtesting, optimization, and strategy validation. Features include multi-parameter optimization, walk-forward analysis, performance metrics calculation, and interactive HTML report generation.

## Features

- **Multi-Parameter Optimization**: Optimize combinations of symbols, timeframes, and strategy parameters
- **Walk-Forward Validation**: Robust out-of-sample testing with anchored or rolling windows
- **Performance Metrics**: Comprehensive statistics including Sharpe ratio, Sortino ratio, drawdown, profit factor
- **Interactive Reporting**: Generate HTML reports with candlestick charts, trade markers, and walk-forward analysis
- **Flexible Parameter Types**: Support for numeric ranges, securities, timeframes, and custom classes
- **Parallel Processing**: Multi-threaded optimization for faster results
- **Metric Filtering**: Select top strategies based on custom performance criteria
- **AI-Assisted Debug Mode**: MCP server integration for agentic debugging and analysis

## Architecture

The solution is organized into separate assemblies following separation of concerns:

```
StockSharp.AdvancedBacktest.Core/           # Business logic (strategies, orders, metrics)
StockSharp.AdvancedBacktest.Infrastructure/ # Operations (export, optimization, debug)
StockSharp.AdvancedBacktest.LauncherTemplate/  # Console app template
StockSharp.AdvancedBacktest.DebugEventLogMcpServer/  # MCP server for AI debugging
```

**Dependency Flow**: Infrastructure → Core → StockSharp (submodule)

## Quick Start

### 1. Setup

Clone the repository with submodules:

```bash
git clone --recurse-submodules https://github.com/PunkBASSter/StockSharp.AdvancedBacktest.git
cd StockSharp.AdvancedBacktest
```

If already cloned, initialize submodules:

```bash
git submodule update --init --recursive
```

Build the solution:

```bash
dotnet build StockSharp.AdvancedBacktest.slnx
```

### Package Locking

This solution uses NuGet package lock files (`packages.lock.json`) to ensure reproducible builds. The StockSharp submodule uses floating versions (e.g., `8.*`, `10.*`), so lock files prevent accidental breakage from upstream package updates.

**Normal workflow**: Just build normally. Restore will fail if lock files are out of sync with resolved packages.

**To update packages to latest versions**:

```powershell
.\Update-PackageLocks.ps1
```

This script:
- Fetches the latest package versions matching version constraints
- Rebuilds the solution to verify compatibility
- Updates the lock files if successful

If the build fails after updating, revert with:

```powershell
git checkout -- **/packages.lock.json
```

### 2. Install Package Reference

Add to your project (reference Infrastructure for full functionality):

```xml
<ProjectReference Include="path\to\StockSharp.AdvancedBacktest.Infrastructure\StockSharp.AdvancedBacktest.Infrastructure.csproj" />
```

Or reference Core only for business logic without orchestration:

```xml
<ProjectReference Include="path\to\StockSharp.AdvancedBacktest.Core\StockSharp.AdvancedBacktest.Core.csproj" />
```

## Usage Guide

### Creating a Strategy

See `StockSharp.AdvancedBacktest.LauncherTemplate` as a strategy launch configuration template.

### Running Tests

```bash
# Run all tests
dotnet test StockSharp.AdvancedBacktest.slnx

# Run specific test projects
dotnet test StockSharp.AdvancedBacktest.Core.Tests/
dotnet test StockSharp.AdvancedBacktest.Infrastructure.Tests/
dotnet test StockSharp.AdvancedBacktest.Tests/
```

## License

This project extends [StockSharp](https://github.com/StockSharp/StockSharp), which is licensed under the GNU LGPL v3.
