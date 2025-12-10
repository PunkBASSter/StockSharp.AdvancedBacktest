# MVP Architecture Diagram

## Assembly Structure

```mermaid
flowchart TB
  subgraph Core["StockSharp.AdvancedBacktest.Core"]
    direction TB
    CS[CustomStrategyBase]
    PM[Parameters: ICustomParam, NumberParam, SecurityParam...]
    OM[OrderManagement: TradeSignal, OrderPositionManager]
    SM[Strategy Modules: PositionSizing, StopLoss, TakeProfit]
    ST[Statistics: PerformanceMetrics, Calculator]
    MO[Models: OptimizationConfig, BacktestConfig, Results]
    WF[PerformanceValidation: WalkForwardConfig, Results]
  end

  subgraph Infra["StockSharp.AdvancedBacktest.Infrastructure"]
    direction TB
    BR[BacktestRunner]
    OR[OptimizerRunner]
    WV[WalkForwardValidator]
    EX[Export: ReportBuilder, BacktestExporter]
    DM[DebugMode: EventLogging, McpServer]
    SR[Storages: SharedStorageRegistry]
    SZ[Serialization: JSON converters]
  end

  SS[StockSharp Submodule]
  APP[Application Projects]

  SS --> Core
  Core --> Infra
  Infra --> APP
```

## Optimization & Validation Flow

```mermaid
flowchart TB
  A[Generate params grid from range/step/type] --> D
  D -->|parallel| B1
  B1 --> F
  F --> N[Selected param sets with report and artifacts]

  subgraph O[Optimization]
    direction LR
    D{OPT: For each combination: params x assets x timeframes x period}
    B1[Optimization Backtest]
  end

  F[Performance filter & rank with optional reports]

  subgraph VB[Validation Backtest]
    direction LR
    D1{ VB: For each combination: params x assets x timeframes x period}
    D1 --> B2[Validation Backtest]
  end

  S .-> B1
  S .-> B2

  subgraph S[Backtest Flow]
    direction LR
    S1[Inputs: start-end period, hyperparameters, assets, timeframes] --> S2[Execute strategy and evaluate]
    S2 --> S3[Outputs: params, metrics, equity curve, trades, logs]
  end

  N --> D1

  B2 --> F1[Performance filter & rank & cross-phase comparison with reports ]
  PR[ParamRanges] --> A
```

## Component Mapping

| Component | Assembly | Responsibility |
|-----------|----------|----------------|
| CustomStrategyBase | Core | Base class for trading strategies |
| Strategy Modules | Core | Position sizing, stop-loss, take-profit calculators |
| OrderPositionManager | Core | Order and position tracking logic |
| TradeSignal | Core | Signal generation and representation |
| PerformanceMetrics | Core | Metric calculation (Sharpe, drawdown, etc.) |
| Parameters System | Core | ICustomParam, NumberParam, SecurityParam |
| BacktestRunner | Infrastructure | Backtest orchestration |
| OptimizerRunner | Infrastructure | Parallel optimization coordination |
| WalkForwardValidator | Infrastructure | Walk-forward analysis orchestration |
| ReportBuilder | Infrastructure | HTML/JSON report generation |
| EventLogging | Infrastructure | Debug event persistence (SQLite) |
| McpServer | Infrastructure | AI agentic debugging interface |
