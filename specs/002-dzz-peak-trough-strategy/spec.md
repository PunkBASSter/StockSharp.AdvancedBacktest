# Feature Specification: DeltaZz Peak/Trough Breakout Strategy

**Feature Branch**: `002-dzz-peak-trough-strategy`
**Created**: 2025-12-25
**Status**: Draft
**Input**: User description: "Build a strategy analogous to ZigZagBreakoutStrategy but using separate DeltaZzPeak and DeltaZzTrough indicators for frontend visualization compatibility. Include a signal deduplication system based on Entry, SL, and TP prices. Extract existing ZigZagBreakout launching code to a dedicated Launcher class and implement a similar launcher for the new strategy with DI container integration."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Breakout Signals from Peak/Trough Indicators (Priority: P1)

As a trader running backtests, I want the strategy to generate the same breakout trading signals as ZigZagBreakoutStrategy but using the separate DeltaZzPeak and DeltaZzTrough indicators, so that I can visualize peaks and troughs as separate chart series while maintaining identical trading logic.

**Why this priority**: This is the core functionality - without correct signal generation from the split indicators, the strategy has no value. The signals must match the original ZigZagBreakoutStrategy behavior.

**Independent Test**: Can be fully tested by running the strategy against historical data and comparing generated signals (entry price, stop-loss, take-profit) against the original ZigZagBreakoutStrategy output.

**Acceptance Scenarios**:

1. **Given** a sequence of candles that produces a breakout signal in ZigZagBreakoutStrategy, **When** the same sequence is processed by the new strategy, **Then** an equivalent breakout signal is generated with matching entry, SL, and TP prices.
2. **Given** the DeltaZzPeak indicator emits a peak value, **When** the strategy combines it with trough history, **Then** the pattern matching logic correctly identifies breakout opportunities.
3. **Given** the DeltaZzTrough indicator emits a trough value, **When** the strategy combines it with peak history, **Then** the pattern matching logic correctly identifies breakout opportunities.

---

### User Story 2 - Deduplicate Repeated Signals (Priority: P1)

As a trader, I want duplicate signals to be filtered out based on Entry, SL, and TP price combinations, so that the same trade opportunity is not registered multiple times when both Peak and Trough indicators emit values that persist across multiple candles.

**Why this priority**: Signal deduplication is essential because Peak/Trough indicators emit values that persist (unlike the combined ZigZag which alternates). Without deduplication, the same breakout setup could trigger multiple orders.

**Independent Test**: Can be tested by feeding candle sequences where indicator values persist for multiple bars and verifying only one order request is generated per unique (Entry, SL, TP) combination.

**Acceptance Scenarios**:

1. **Given** a signal with specific Entry/SL/TP prices was already generated, **When** the same signal conditions persist on subsequent candles, **Then** no additional order request is created.
2. **Given** indicator values persist but market conditions change the calculated Entry/SL/TP, **When** the new prices differ from the previous signal, **Then** a new order request is generated.
3. **Given** a position was opened from a previous signal, **When** the same Entry/SL/TP signal reappears after position close, **Then** the signal is treated as new (not deduplicated).

---

### User Story 3 - Extract ZigZagBreakout Launcher (Priority: P1)

As a developer maintaining the LauncherTemplate, I want the ZigZagBreakoutStrategy-specific launching code extracted from Program.cs into a dedicated launcher class, so that the main program remains a thin orchestrator and each strategy has its own self-contained configuration.

**Why this priority**: This refactoring is a prerequisite for adding the new strategy launcher. The existing monolithic Program.cs makes it difficult to add new strategies without code duplication.

**Independent Test**: Can be tested by running the existing backtest with the refactored code and verifying identical results (same trades, same metrics, same reports).

**Acceptance Scenarios**:

1. **Given** the existing Program.cs with embedded ZigZagBreakout configuration, **When** refactored to use a dedicated launcher, **Then** backtest results remain identical.
2. **Given** a ZigZagBreakout launcher class, **When** the Program.cs invokes it, **Then** the launcher handles all strategy-specific setup (security, portfolio, parameters, timeframes).
3. **Given** the launcher abstraction, **When** adding a new strategy, **Then** only a new launcher implementation is needed without modifying Program.cs core logic.

---

### User Story 4 - DzzPeakTrough Strategy Launcher with DI (Priority: P1)

As a developer, I want a dedicated launcher for the DzzPeakTrough strategy that uses dependency injection to create and configure the strategy instance, so that dependencies are explicit, testable, and follow modern .NET patterns.

**Why this priority**: DI integration enables proper unit testing of the launcher, explicit dependency management, and follows established patterns for .NET applications.

**Independent Test**: Can be tested by resolving the strategy from the DI container and verifying all dependencies are correctly injected.

**Acceptance Scenarios**:

1. **Given** the DI container is configured, **When** resolving the DzzPeakTrough strategy, **Then** all required dependencies (position sizer, order manager factory, etc.) are injected.
2. **Given** the DzzPeakTrough launcher, **When** running a backtest, **Then** the launcher configures all settings (security, portfolio, parameters, debug mode) and executes the backtest.
3. **Given** the launcher with DI, **When** writing unit tests, **Then** dependencies can be mocked/stubbed without modifying the launcher code.

---

### User Story 5 - Frontend Visualization Compatibility (Priority: P2)

As a frontend developer integrating backtest results, I want the strategy to register separate DeltaZzPeak and DeltaZzTrough indicators, so that they can be exported and rendered as two distinct chart series without the challenge of drawing a single line from two alternating buffers.

**Why this priority**: This enables the visualization use case that motivated the split indicators. The strategy must expose both indicators for the export pipeline to capture.

**Independent Test**: Can be tested by running the strategy and verifying both DeltaZzPeak and DeltaZzTrough appear in the Indicators collection with their respective values exported.

**Acceptance Scenarios**:

1. **Given** the strategy is initialized, **When** checking the Indicators collection, **Then** both DeltaZzPeak and DeltaZzTrough indicators are registered.
2. **Given** a backtest completes, **When** indicator data is exported, **Then** peak values and trough values are in separate series with non-overlapping timestamps.

---

### User Story 6 - Order Management Integration (Priority: P2)

As a trader, I want the strategy to integrate with the existing OrderPositionManager, so that entry orders, stop-losses, and take-profits are handled consistently with other strategies in the framework.

**Why this priority**: Reusing the existing order management infrastructure ensures consistency and reduces implementation risk.

**Independent Test**: Can be tested by verifying that orders flow through OrderPositionManager and protective levels (SL/TP) trigger correctly.

**Acceptance Scenarios**:

1. **Given** a breakout signal is generated, **When** an order request is created, **Then** it is processed through OrderPositionManager with the specified Entry, SL, and TP.
2. **Given** an open position with protective levels set, **When** price hits the stop-loss level, **Then** the position is closed at or near the SL price.

---

### Edge Cases

- What happens when Peak and Trough emit values on the same candle? The pattern matching should handle both updates in sequence.
- How does the system handle the initial candles before enough history exists? The strategy should wait until sufficient zigzag points are available (minimum 3 non-empty values as in the original).
- What happens when the deduplication cache grows large over extended backtests? The cache should clear entries after positions close or signals expire.
- What happens if DI container resolution fails due to missing registrations? Clear error messages should indicate which dependency is missing.

## Requirements *(mandatory)*

### Functional Requirements

#### Strategy Logic
- **FR-001**: System MUST use DeltaZzPeak and DeltaZzTrough indicators instead of DeltaZigZag for signal detection while producing equivalent breakout signals.
- **FR-002**: System MUST maintain synchronized history of peak and trough values to reconstruct the zigzag pattern for signal detection.
- **FR-003**: System MUST deduplicate order requests based on the combination of Entry price, Stop-Loss price, and Take-Profit price.
- **FR-004**: System MUST reset the deduplication state when a position is closed (allowing the same signal to trigger a new entry).
- **FR-005**: System MUST register both DeltaZzPeak and DeltaZzTrough in the Indicators collection for export/visualization.
- **FR-006**: System MUST integrate with OrderPositionManager for order lifecycle management.
- **FR-007**: System MUST apply the same breakout pattern logic as ZigZagBreakoutStrategy (price > sl, l1 < price).
- **FR-008**: System MUST calculate position size using the existing IRiskAwarePositionSizer infrastructure.
- **FR-009**: System MUST check protective levels (SL/TP) before evaluating new signals on each candle.

#### Launcher Infrastructure
- **FR-010**: System MUST extract ZigZagBreakout-specific configuration from Program.cs into a dedicated ZigZagBreakoutLauncher class.
- **FR-011**: System MUST define a common launcher abstraction (interface or base class) that both ZigZagBreakout and DzzPeakTrough launchers implement.
- **FR-012**: System MUST implement a DzzPeakTroughLauncher that configures and creates the new strategy instance.
- **FR-013**: Launchers MUST configure: security definition, portfolio, strategy parameters, timeframes, and debug mode settings.
- **FR-014**: Launchers MUST register strategy dependencies in the DI container for resolution.
- **FR-015**: Program.cs MUST use the DI container to resolve and execute the selected launcher.
- **FR-016**: System MUST support selecting which launcher/strategy to run (via command-line argument or configuration).

### Key Entities

- **DzzPeakTroughHistory**: Maintains ordered history of peak and trough values with timestamps for pattern matching. Combines values from both indicators into a unified sequence.
- **SignalDeduplicator**: Tracks previously generated signals by (Entry, SL, TP) tuple to prevent duplicate order requests. Clears on position close.
- **OrderRequest**: Existing entity representing an entry order with associated protective pair (SL/TP).
- **IStrategyLauncher**: Common abstraction for strategy launchers defining the contract for configuration and execution.
- **ZigZagBreakoutLauncher**: Extracted launcher for the existing ZigZagBreakout strategy.
- **DzzPeakTroughLauncher**: New launcher for the DzzPeakTrough strategy with full DI integration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Strategy produces identical trade signals to ZigZagBreakoutStrategy when run against the same historical data (100% signal match rate).
- **SC-002**: No duplicate orders are generated when indicator values persist across consecutive candles.
- **SC-003**: Both DeltaZzPeak and DeltaZzTrough indicator series are available in exported backtest data with correct values.
- **SC-004**: Strategy passes all unit tests covering signal generation, deduplication, and order management integration.
- **SC-005**: Refactored ZigZagBreakoutLauncher produces identical backtest results to the original Program.cs implementation.
- **SC-006**: All launcher dependencies can be resolved from the DI container without runtime errors.
- **SC-007**: Launcher unit tests can execute with mocked dependencies (no external system dependencies required).

## Assumptions

- The DeltaZzPeak and DeltaZzTrough indicators correctly filter peaks and troughs from the underlying DeltaZigZag logic (verified by existing indicator tests).
- The OrderPositionManager API remains stable and supports the required order request/protective pair workflow.
- Position size calculation via IRiskAwarePositionSizer is already implemented and tested.
- The strategy only supports long (buy) positions as in the original ZigZagBreakoutStrategy.
- Microsoft.Extensions.DependencyInjection is acceptable for DI container implementation (standard .NET package).
- The existing BacktestRunner and related infrastructure will be used without modification.
