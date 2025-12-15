# Feature Specification: Isolated Order Groups with Split Position Management

**Feature Branch**: `004-isolated-order-groups`
**Created**: 2025-12-15
**Status**: Draft
**Input**: User description: "Reconsidered approach of handling positions with isolated order groups to allow multiple concurrent positions, split SL/TP pairs for flexible exits, partial fill handling with market close retries, and auxiliary timeframe for order maintenance without debug mode export."

## Clarifications

### Session 2025-12-15

- Q: What states should the order group track through its lifecycle? → A: Standard 4-state model: Pending → EntryFilled → ProtectionActive → Closed
- Q: Should there be a limit on concurrent order groups? → A: Configurable limit with default of 5
- Q: How to determine SL/TP trigger order when both hit in same candle? → A: Auxiliary timeframe provides granularity; fallback: always trigger SL first (pessimistic)
- Q: Should auxiliary TF affect regular reporting/charts? → A: No, charts and reports use main TF only; auxiliary TF is excluded from all exports
- Q: Should debug modes be unified? → A: Yes, unify AI debug and human debug under one abstraction; both can be enabled simultaneously and independently
- Q: What is the visibility scope of auxiliary TF? → A: Completely invisible - internal implementation detail only; exists solely to improve backtest modeling quality for position maintenance
- Q: How should timestamps from auxiliary TF events be handled? → A: Use auxiliary TF timestamps internally for correct chronological ordering, but map/attribute events to the parent main TF candle for display (e.g., event at 1:15 displays under 1:00 candle for hourly TF)
- Q: What criteria should be used to match/detect duplicate order signals? → A: Match by entry price + SL + TP (all three values must match within price step tolerances) to detect true duplicates
- Q: Should position management code be strategy-specific or reusable? → A: Must be reusable by other strategies with generic and flexible API design

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Auxiliary Timeframe for Order Maintenance (Priority: P1)

As a strategy developer, I want order/position maintenance to occur more frequently (every 5 minutes) than the main strategy timeframe (hourly) so that stops and targets are checked more often during backtesting.

**Why this priority**: This is the foundational prerequisite. The current hourly candle-based execution misses intra-bar price movements, leading to inaccurate SL/TP triggering in backtests. Must be in place before any order management can function correctly.

**Independent Test**: Can be fully tested by running a backtest with hourly candles and verifying that order maintenance logic executes at 5-minute intervals based on a separate subscription.

**Acceptance Scenarios**:

1. **Given** a strategy subscribed to 1-hour candles, **When** the strategy initializes, **Then** an auxiliary 5-minute subscription is created for order maintenance.
2. **Given** an active position with SL at 95, **When** the 5-minute candle shows price touching 95, **Then** the SL check triggers even if the hourly candle hasn't completed.
3. **Given** the auxiliary 5-minute subscription is active, **When** any output is generated (debug, charts, reports, logs), **Then** the auxiliary timeframe is completely invisible - no trace in any output.
4. **Given** a stop-loss triggers at 1:15 (auxiliary TF event), **When** the event is displayed in charts/reports, **Then** it is attributed to the 1:00 main TF candle while preserving correct chronological order internally.

---

### User Story 2 - Order Group Registry and State Tracking (Priority: P1)

As a strategy developer, I want a centralized registry that tracks all order groups and their states so that I can query active orders, compare signals, and manage order lifecycle.

**Why this priority**: This is foundational infrastructure that must exist before order management features can be built. Depends on Auxiliary Timeframe (P1) for proper event handling.

**Independent Test**: Can be fully tested by registering multiple order groups and querying/filtering them by various criteria (state, price levels, volume).

**Acceptance Scenarios**:

1. **Given** no existing order groups, **When** a new entry order is placed, **Then** it is registered in the OrderRegistry with a unique group ID.
2. **Given** an existing order group in the registry, **When** a similar signal is received (same entry price + SL + TP levels within price step tolerances), **Then** the system can detect the duplicate and avoid creating redundant orders.
3. **Given** multiple order groups in various states, **When** querying for active groups, **Then** only groups with pending or filled entries (and unfilled exits) are returned.

---

### User Story 3 - Multiple Concurrent Positions (Priority: P2)

As a strategy developer, I want to open multiple positions simultaneously so that I don't miss trading signals while existing positions have far-away stop-loss or take-profit levels.

**Why this priority**: This is the core problem being solved. Depends on OrderRegistry (P1) to track multiple groups and Auxiliary Timeframe (P1) for proper SL/TP checking.

**Independent Test**: Can be fully tested by generating multiple sequential signals and verifying each creates an independent order group that tracks its own entry and exits.

**Acceptance Scenarios**:

1. **Given** an existing open position with SL at -5% and TP at +10%, **When** a new valid trading signal is detected, **Then** a new independent order group is created and the entry order is placed.
2. **Given** two active order groups with different entry prices, **When** the SL of group A is triggered, **Then** only group A's position is closed while group B remains active.
3. **Given** an active order group with pending entry, **When** the signal changes for a new group, **Then** the existing pending group is updated/cancelled while other filled groups remain unaffected.

---

### User Story 4 - Split Exit Orders with Multiple SL/TP Pairs (Priority: P2)

As a strategy developer, I want to define multiple protective pairs (SL/TP) for a single entry so that I can implement partial exit strategies (e.g., exit 50% at TP1 based on ZigZag, exit remaining 50% at TP2 based on ATR).

**Why this priority**: Enables sophisticated position management. Depends on OrderRegistry (P1) and builds on Multiple Concurrent Positions (P2).

**Independent Test**: Can be fully tested by entering a position with volume 1.0, defining two protective pairs (each with volume 0.5), and verifying that when one TP is hit, that portion closes and the other pair's TP is cancelled.

**Acceptance Scenarios**:

1. **Given** an entry order with volume 1.0 and two protective pairs (SL: 95, TP1: 105, volume: 0.5) and (SL: 95, TP2: 110, volume: 0.5), **When** the entry is filled, **Then** four protective orders are placed (2 SL orders, 2 TP orders) with correct volumes.
2. **Given** two active protective pairs with the same SL but different TPs, **When** TP1 at 105 is hit, **Then** the 0.5 volume is closed, the corresponding SL order is cancelled, and the second pair (SL, TP2) remains active.
3. **Given** protective pairs where one pair's TP is partially filled, **When** the partial fill occurs, **Then** the system updates the remaining volume and adjusts the cancellation logic accordingly.

---

### User Story 5 - Partial Fill Handling with Market Close Retry (Priority: P3)

As a strategy developer, I want the system to handle partial fills gracefully by retrying to close the remaining volume via market orders so that I don't have orphaned positions.

**Why this priority**: Enhancement for robustness. Partial fills are common in real trading but can be addressed after core order management is working.

**Independent Test**: Can be fully tested by simulating a partial fill of a protective order and verifying the retry mechanism attempts market close up to 5 times.

**Acceptance Scenarios**:

1. **Given** a protective order for volume 0.5 is partially filled (0.3 filled, 0.2 remaining), **When** the fill event is processed, **Then** the system initiates a market order for the remaining 0.2 volume.
2. **Given** a market order retry for remaining volume fails, **When** the retry count is less than 5, **Then** the system retries the market order.
3. **Given** a market order retry for remaining volume fails 5 times, **When** the 5th retry fails, **Then** the system logs an error and marks the order group as requiring manual intervention.
4. **Given** a market order retry succeeds, **When** the remaining volume is filled, **Then** the order group is properly closed and the opposing protective orders are cancelled.

---

### Edge Cases

- What happens when the strategy is reconnected in live trading with existing open positions? The registry should be able to reconstruct state from connector's order/position data.
- How does the system handle a candle where both SL and TP could theoretically trigger? The auxiliary timeframe (5-min) provides finer granularity to detect which triggered first. If both still fall within the same auxiliary candle, use pessimistic fallback: always trigger SL first.
- What happens when all 5 market order retries fail? Log error and maintain position tracking for manual intervention.
- How are protective orders handled when the entry order expires without filling? Cancel all associated protective orders in the group.
- What happens if volume validation fails (protective pair volumes don't sum to entry volume)? Reject the order request with a validation error before placing.
- How to ensure auxiliary TF timestamp mapping doesn't break event ordering? Maintain internal chronological order using actual timestamps; only remap display/attribution to parent main TF candle. Verify no duplicate or out-of-order events in outputs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support multiple concurrent order groups, each tracking an independent entry and its associated protective orders.
- **FR-002**: System MUST allow defining multiple protective pairs (SL/TP combinations) per entry order, with volumes that sum to the entry volume.
- **FR-003**: System MUST cancel the corresponding protective order when its pair order fills (e.g., when TP1 fills, cancel SL1).
- **FR-004**: System MUST handle partial fills by attempting to close remaining volume via market orders with up to 5 retry attempts.
- **FR-005**: System MUST provide an auxiliary timeframe subscription (5-minute default) for order maintenance that is independent of the main strategy candle subscription.
- **FR-006**: System MUST treat auxiliary timeframe as completely invisible internal implementation detail. It MUST NOT appear in any output: debug modes, charts, reports, logs, or any user-facing data. It exists solely as an event source to improve backtest modeling quality for position maintenance.
- **FR-006a**: System MUST use auxiliary TF timestamps internally to ensure correct chronological ordering of events.
- **FR-006b**: System MUST map/attribute all events triggered by auxiliary TF to the corresponding parent main TF candle for display purposes (e.g., SL triggered at 1:15 is displayed under the 1:00 hourly candle).
- **FR-007**: System MUST maintain an OrderRegistry that tracks all order groups with their current states and allows querying by various criteria.
- **FR-007a**: OrderRegistry MUST provide a method to find matching order groups by comparing entry price + SL + TP levels (all three) within price step tolerances.
- **FR-008**: System MUST validate that protective pair volumes sum to entry order volume before placing orders.
- **FR-009**: System MUST support both limit orders and market orders for protective orders based on configuration.
- **FR-010**: System MUST cancel pending entry orders when signal changes, without affecting other filled order groups.
- **FR-011**: System MUST provide a Reset() method to clear all order group state when strategy resets.
- **FR-012**: System MUST log state transitions and order events for debugging purposes.
- **FR-013**: System MUST enforce a configurable maximum number of concurrent order groups (default: 5) and reject new entries when the limit is reached.
- **FR-014**: System MUST use pessimistic trigger order (SL before TP) as fallback when both levels are hit within the same auxiliary timeframe candle.
- **FR-015**: System MUST provide a unified debug mode abstraction that supports both AI agentic debug and human debug as independently configurable options that can be enabled simultaneously.
- **FR-016**: All position management components (OrderRegistry, OrderPositionManager, OrderRequest, ProtectivePair) MUST be designed as reusable, strategy-agnostic APIs that can be consumed by any strategy implementation without modification.

### Key Entities

- **OrderRequest**: Represents an entry order with its list of protective pairs (SL/TP/volume combinations).
- **ProtectivePair**: A single SL/TP pair with associated volume, linked to an entry order.
- **EntryOrderGroup**: An entry order with a dictionary of protective pairs, tracking the lifecycle of one position. States: Pending → EntryFilled → ProtectionActive → Closed.
- **OrderRegistry**: Central registry managing all order groups for a strategy, supporting queries and state reconstruction.
- **OrderPositionManager**: Orchestrates order placement, fill handling, cancellation logic, and protection level checking.
- **DebugModeProvider**: Unified abstraction for debug modes; supports AI debug and human debug as independent, simultaneously-enabled options; filters auxiliary TF events from all outputs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Strategy can maintain up to 5 concurrent positions (configurable) with independent SL/TP management without interference.
- **SC-002**: Partial exits execute correctly with volume accuracy (protective pair volume matches closed amount within price step tolerance).
- **SC-003**: Partial fill retry mechanism closes remaining volume within 5 attempts in 95%+ of cases during backtesting.
- **SC-004**: Auxiliary timeframe maintenance checks occur at 5-minute intervals (12x more frequently than hourly candles).
- **SC-005**: Auxiliary timeframe is completely invisible - zero presence in any output (debug modes, charts, reports, logs, UI).
- **SC-006**: All existing OrderPositionManager tests continue to pass after refactoring.
- **SC-007**: New unit tests cover all FR requirements with at least one test per functional requirement.
- **SC-008**: Both AI debug and human debug modes can run simultaneously without interference when both are enabled.
- **SC-009**: Events triggered by auxiliary TF are correctly attributed to parent main TF candle in all outputs, with no duplicate or out-of-order events.
- **SC-010**: Position management APIs are strategy-agnostic - no strategy-specific code or dependencies in OrderRegistry, OrderPositionManager, or related components.

## Assumptions

- The StockSharp backtesting engine supports multiple concurrent candle subscriptions at different timeframes.
- The auxiliary timeframe (5 minutes) is sufficient granularity for realistic SL/TP triggering in backtests.
- The 5-retry limit for partial fills is adequate for backtest scenarios; live trading may need different handling.
- Protective orders use the same security as the entry order.
- Order group comparison for duplicate detection uses entry price + SL + TP levels (all three must match) within one price step tolerance.
