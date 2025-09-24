# Moving Average Crossover Strategy Backtester

A cross-platform console application for backtesting and optimizing a simple Moving Average Crossover strategy on BTCUSDT@BNB (Binance) H1 data using StockSharp.

## Overview

This application allows you to:

1. Backtest a Moving Average Crossover strategy on historical data
2. Optimize strategy parameters using either Brute Force or Genetic algorithms
3. Generate comprehensive performance reports
4. Export results in multiple formats (CSV, JSON, text)

## Features

- **Strategy Implementation**: Simple Moving Average (SMA) crossover with configurable fast and slow periods
- **Optimization Methods**: Both brute force and genetic algorithm approaches
- **Risk Management**: Configurable stop-loss and take-profit settings
- **Performance Metrics**: Comprehensive metrics including Sharpe ratio, Sortino ratio, drawdown, win rate, etc.
- **Cross-Platform**: Built on .NET 6.0 for Windows, Linux, and macOS compatibility

## Prerequisites

- .NET 6.0 SDK or later
- StockSharp libraries (included via NuGet)
- Historical data for BTCUSDT@BNB (Binance)

## Usage

### Command Line Options

```
MaCrossoverBacktester [options]

Options:
  --optimization-type <type>     Type of optimization (BruteForce or Genetic) [default: BruteForce]
  --start-date <date>            Start date for backtesting (yyyy-MM-dd) [default: one year ago]
  --end-date <date>              End date for backtesting (yyyy-MM-dd) [default: today]
  --fast-ma-min <value>          Minimum period for Fast MA [default: 5]
  --fast-ma-max <value>          Maximum period for Fast MA [default: 50]
  --fast-ma-step <value>         Step for Fast MA period [default: 5]
  --slow-ma-min <value>          Minimum period for Slow MA [default: 50]
  --slow-ma-max <value>          Maximum period for Slow MA [default: 200]
  --slow-ma-step <value>         Step for Slow MA period [default: 10]
  --history-path <path>          Path to historical data [default: ./HistoryData]
  --output-path <path>           Path for results output [default: ./Results]
  --initial-capital <amount>      Initial capital for backtesting [default: 10000]
  --volume <amount>              Volume per trade in BTC [default: 0.01]
  --batch-size <size>            Number of parallel threads (0 = auto) [default: 0]
  --max-iterations <number>      Maximum number of iterations [default: 1000]
  --stop-loss <percent>          Stop loss percentage (0 = disabled) [default: 2.0]
  --take-profit <percent>        Take profit percentage (0 = disabled) [default: 4.0]
```

### Example Commands

Basic brute force optimization with default parameters:
```
dotnet run
```

Genetic optimization with custom parameters:
```
dotnet run --optimization-type Genetic --fast-ma-min 10 --fast-ma-max 60 --slow-ma-min 60 --slow-ma-max 240 --initial-capital 5000
```

Specific date range:
```
dotnet run --start-date 2022-01-01 --end-date 2022-12-31
```

### Historical Data

This application expects historical data in the StockSharp format. You can:

1. Use Hydra to download historical data
2. Download data from Binance directly using StockSharp connectors
3. Place pre-downloaded data in the configured history folder

### Output Files

After optimization, the following files will be created in the output directory:

- `optimization_results_[timestamp].csv` - Detailed results in CSV format
- `optimization_results_[timestamp].json` - Machine-readable format in JSON
- `optimization_results_[timestamp]_summary.txt` - Human-readable summary
- `optimization_results_[timestamp]_best.txt` - Top 10 parameter combinations

## Strategy Logic

The Moving Average Crossover strategy is based on the following rules:

1. When the fast MA crosses above the slow MA, go long (buy signal)
2. When the fast MA crosses below the slow MA, go short or exit long (sell signal)
3. Optional stop-loss and take-profit can be applied

## Performance Metrics

The application calculates the following performance metrics:

- Total Return (%)
- Annualized Return (%)
- Sharpe Ratio
- Sortino Ratio
- Maximum Drawdown (%)
- Win Rate (%)
- Profit Factor
- Number of Trades
- Average Win/Loss

## Development

The application is built using:

- .NET 6.0
- StockSharp Framework
- System.CommandLine for CLI

## License

This project is released under the terms of the StockSharp license.