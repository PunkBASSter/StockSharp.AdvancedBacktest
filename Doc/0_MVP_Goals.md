# Advanced Backtesting with StockSharp

## MVP Goals

- Allow optimizing dynamically plugged strategy module combinations.
- Allow optimization iterating multiple symbols and timeframes.
- Run continuous validation with best runs selection by calculated metrics (e.g., Sharpe ratio, maximum drawdown, Sortino ratio and etc.).
- Export optimization results to local file system as JSON for web visualization.
- Visualize backtest run results in an interactive web-based report including candlestick chart with trades (with direction/price/SL/TP) for single symbol strategies, equity curve and statistical metrics table.
- Allow launching strategies with settings import from JSON files as a cross-platform console application.

## Stretch Goals

- Prevent overfitting during validation (montecarlo permutations testing, other possible methods).
- Implement optimization results partitioning by Symbol, Timeframe, history period and Strategy configuration.
- Export and save partitioned optimization results in various formats (e.g., CSV, JSON, Parquet) to various destinations (e.g., local file system, cloud storage).
- Support hosting prod strategies as schedule-triggered AWS Lambda functions.
- Visualize indicators data associated with the symbol price chart.
- Visualize multi-symbol strategies on chart with multiple panes.
