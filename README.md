# StockSharp.AdvancedBacktest

A .NET 10 library extending StockSharp for advanced backtesting, optimization, and strategy validation. Features include multi-parameter optimization, walk-forward analysis, performance metrics calculation, and interactive HTML report generation.

## Features

- **Multi-Parameter Optimization**: Optimize combinations of symbols, timeframes, and strategy parameters
- **Walk-Forward Validation**: Robust out-of-sample testing with anchored or rolling windows
- **Performance Metrics**: Comprehensive statistics including Sharpe ratio, Sortino ratio, drawdown, profit factor
- **Interactive Reporting**: Generate HTML reports with candlestick charts, trade markers, and walk-forward analysis
- **Flexible Parameter Types**: Support for numeric ranges, securities, timeframes, and custom classes
- **Parallel Processing**: Multi-threaded optimization for faster results
- **Metric Filtering**: Select top strategies based on custom performance criteria

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

### 2. Install Package Reference

Add to your project:

```xml
<ProjectReference Include="path\to\StockSharp.AdvancedBacktest\StockSharp.AdvancedBacktest.csproj" />
```

## Usage Guide

### Creating a Strategy

See `StockSharp.AdvancedBacktest.LauncherTemplate` as a strategy launch configuration.

## License

This project extends [StockSharp](https://github.com/StockSharp/StockSharp), which is licensed under the GNU LGPL v3.
