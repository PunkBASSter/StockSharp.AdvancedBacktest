# Data Model: DeltaZz Peak/Trough Breakout Strategy

**Date**: 2025-12-25
**Status**: Complete

## Entities

### 1. SignalKey (New - Core)

**Purpose**: Immutable key for signal deduplication based on price levels.

```text
SignalKey (record)
├── EntryPrice: decimal    # Entry limit order price
├── StopLoss: decimal      # Stop-loss price level
└── TakeProfit: decimal    # Take-profit price level
```

**Validation Rules**:
- All prices must be > 0
- StopLoss < EntryPrice < TakeProfit (for long positions)

**State Transitions**: N/A (immutable record)

---

### 2. SignalDeduplicator (New - Core)

**Purpose**: Tracks last signal to prevent duplicate order generation.

```text
SignalDeduplicator
├── _lastSignal: SignalKey?   # Most recent signal (null = no signal)
└── Methods:
    ├── IsDuplicate(entry, sl, tp): bool
    └── Reset(): void
```

**Validation Rules**:
- IsDuplicate returns true only if all three prices match exactly

**State Transitions**:
```
[Initial] --IsDuplicate(new)--> [HasSignal]
[HasSignal] --IsDuplicate(same)--> [HasSignal] (returns true)
[HasSignal] --IsDuplicate(different)--> [HasSignal] (updates, returns false)
[HasSignal] --Reset()--> [Initial]
```

---

### 3. DzzPeakTroughConfig (New - LauncherTemplate)

**Purpose**: Configuration for the DzzPeakTrough strategy.

```text
DzzPeakTroughConfig
├── DzzDepth: decimal          # Delta parameter for DeltaZigZag (default: 5)
├── RiskPercentPerTrade: decimal # Risk per trade as percentage (default: 1)
├── MinPositionSize: decimal   # Minimum order volume (default: 0.01)
├── MaxPositionSize: decimal   # Maximum order volume (default: 10)
└── MinimumThreshold: decimal? # Override for DeltaZigZag MinimumThreshold (optional)
```

**Validation Rules**:
- DzzDepth: 0 < value <= 100
- RiskPercentPerTrade: 0 < value <= 10
- MinPositionSize: > 0
- MaxPositionSize: >= MinPositionSize

---

### 4. DzzPeakTroughStrategy (New - LauncherTemplate)

**Purpose**: Strategy implementation using separate Peak/Trough indicators.

```text
DzzPeakTroughStrategy : CustomStrategyBase
├── _peakIndicator: DeltaZzPeak
├── _troughIndicator: DeltaZzTrough
├── _config: DzzPeakTroughConfig?
├── _orderManager: OrderPositionManager?
├── _positionSizer: IRiskAwarePositionSizer?
├── _signalDeduplicator: SignalDeduplicator
├── _dzzHistory: List<(decimal value, bool isUp, DateTimeOffset time)>
└── Methods:
    ├── GetWorkingSecurities(): IEnumerable<(Security, DataType)>
    ├── OnStarted2(time): void
    ├── OnProcessCandle(candle): void
    ├── TryGetBuyOrder(): (decimal price, decimal sl, decimal tp)?
    ├── CalculatePositionSize(entry, sl): decimal
    └── OnOwnTradeReceived(trade): void
```

**Relationships**:
- Uses DeltaZzPeak and DeltaZzTrough indicators (composition)
- Uses OrderPositionManager for order lifecycle
- Uses SignalDeduplicator to filter duplicate signals
- Uses IRiskAwarePositionSizer for volume calculation

---

### 5. IStrategyLauncher (New - LauncherTemplate)

**Purpose**: Abstraction for strategy launchers enabling DI resolution.

```text
IStrategyLauncher (interface)
├── Name: string { get; }    # Display name for CLI
└── RunAsync(aiDebug): Task<int>  # Execute backtest, return exit code
```

---

### 6. ZigZagBreakoutLauncher (New - LauncherTemplate)

**Purpose**: Extracted launcher for existing ZigZagBreakout strategy.

```text
ZigZagBreakoutLauncher : IStrategyLauncher
├── _services: IServiceProvider
├── Name: "ZigZagBreakout"
└── RunAsync(aiDebug): Task<int>
    ├── Creates Security, Portfolio, BacktestConfig
    ├── Creates ZigZagBreakout strategy instance
    ├── Runs BacktestRunner
    └── Generates report
```

---

### 7. DzzPeakTroughLauncher (New - LauncherTemplate)

**Purpose**: Launcher for new DzzPeakTrough strategy with DI integration.

```text
DzzPeakTroughLauncher : IStrategyLauncher
├── _services: IServiceProvider
├── Name: "DzzPeakTrough"
└── RunAsync(aiDebug): Task<int>
    ├── Creates Security, Portfolio, BacktestConfig
    ├── Resolves DzzPeakTroughStrategy from DI
    ├── Runs BacktestRunner
    └── Generates report
```

---

## Entity Relationships

```text
┌─────────────────────────────────────────────────────────────────┐
│                        LauncherTemplate                          │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐     ┌───────────────────────┐              │
│  │ IStrategyLauncher│     │ DzzPeakTroughStrategy │              │
│  └────────┬────────┘     └───────────┬───────────┘              │
│           │                          │                          │
│           │ implements               │ uses                     │
│           ▼                          ▼                          │
│  ┌────────────────────┐    ┌─────────────────────┐              │
│  │ZigZagBreakoutLauncher│   │ OrderPositionManager │◄───────────┤
│  │DzzPeakTroughLauncher │   └─────────────────────┘   (from Core)│
│  └────────────────────┘              │                          │
│                                      │ uses                     │
│                                      ▼                          │
│                            ┌──────────────────┐                 │
│                            │ SignalDeduplicator│◄───────────────┤
│                            └──────────────────┘     (from Core) │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                             Core                                 │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────┐                     │
│  │ SignalDeduplicator│◄──│    SignalKey    │                     │
│  └─────────────────┘    └─────────────────┘                     │
│          │                                                       │
│          │ manages                                               │
│          ▼                                                       │
│  ┌─────────────────────────────────┐                            │
│  │ OrderPositionManager (existing) │                            │
│  └─────────────────────────────────┘                            │
│                                                                  │
│  ┌─────────────────────────────────┐                            │
│  │ Indicators (existing)           │                            │
│  │  ├─ DeltaZigZag                │                            │
│  │  ├─ DeltaZzPeak                │                            │
│  │  └─ DeltaZzTrough              │                            │
│  └─────────────────────────────────┘                            │
└─────────────────────────────────────────────────────────────────┘
```

## Existing Entities (Reference)

### ProtectivePair (Existing - Core)
```text
ProtectivePair (record)
├── StopLossPrice: decimal
├── TakeProfitPrice: decimal
├── Volume: decimal?
└── OrderType: OrderTypes (default: Limit)
```

### OrderRequest (Existing - Core)
```text
OrderRequest (record)
├── Order: Order
└── ProtectivePairs: List<ProtectivePair>
```

### OrderPositionManager (Existing - Core)
```text
OrderPositionManager
├── HandleOrderRequest(request): Order?
├── CheckProtectionLevels(candle): bool
├── OnOwnTradeReceived(trade): void
└── Reset(): void
```
