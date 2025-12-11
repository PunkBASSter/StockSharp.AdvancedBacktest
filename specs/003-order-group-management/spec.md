# Feature Specification: Advanced Order Group Management

**Feature Branch**: `003-order-group-management`
**Created**: 2025-12-11
**Status**: Draft
**Input**: User description: "Advanced order group management system for tracking multiple order groups per security with fractional closing orders, configurable limits, and state tracking"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create Order Group with Multiple Closing Orders (Priority: P1)

A strategy developer needs to enter a position with a single opening order and define multiple closing orders at different price levels to scale out of the position gradually. This enables smoother equity curves by taking partial profits at multiple targets.

**Why this priority**: Core functionality - without the ability to create order groups with multiple closing orders, the entire feature has no value. This is the fundamental building block.

**Independent Test**: Can be fully tested by creating an order group with one opening order and 3 closing orders with fractional volumes, verifying that total closing volume matches opening volume.

**Acceptance Scenarios**:

1. **Given** a strategy with no active positions, **When** the user creates an order group with 1 opening order (100 shares) and 3 closing orders (30, 30, 40 shares), **Then** the order group is created successfully with all orders tracked
2. **Given** a strategy creating an order group, **When** the closing order volumes do not sum to the opening volume and `throwIfNotMatchingVolume` is enabled, **Then** the system throws a validation error
3. **Given** a strategy creating an order group, **When** no GroupId is provided, **Then** the system generates a GroupId in format `SecurityId_DateTimeWithMs_OpenPrice`
4. **Given** a strategy creating an order group, **When** a custom GroupId is provided, **Then** the system uses the provided GroupId

---

### User Story 2 - Opening Order Activation Triggers Closing Orders (Priority: P1)

When an opening order is filled, the associated closing orders must be automatically placed. This ensures protection orders are always in place once a position is established.

**Why this priority**: Critical for risk management - closing orders must be placed immediately after entry to protect the position.

**Independent Test**: Can be tested by creating an order group, simulating the opening order fill, and verifying all closing orders are placed with correct prices and volumes.

**Acceptance Scenarios**:

1. **Given** an order group with a pending opening order, **When** the opening order is fully filled, **Then** all associated closing orders are placed immediately
2. **Given** an order group with a pending opening order, **When** the opening order is partially filled (e.g., 60% of 100 shares), **Then** closing orders are placed using pro-rata scaling (e.g., closing orders [30, 30, 40] become [18, 18, 24])
3. **Given** an order group with closing orders defined, **When** the opening order fills, **Then** the system automatically selects appropriate order types (limit for non-market orders)

---

### User Story 3 - Manage Multiple Simultaneous Order Groups (Priority: P1)

A strategy may have multiple order groups active simultaneously for the same security (e.g., adding to a position at different levels). The system must track all groups independently while respecting configurable limits.

**Why this priority**: Essential for real-world trading strategies that scale into positions over time.

**Independent Test**: Can be tested by creating 3 order groups for the same security and verifying each is tracked independently with correct state.

**Acceptance Scenarios**:

1. **Given** a security with 2 active order groups, **When** a new order group is created within limits, **Then** all 3 groups are tracked independently
2. **Given** configurable max order groups set to 3, **When** a 4th order group creation is attempted, **Then** the system rejects the creation with a limit exceeded error
3. **Given** configurable max risk percentage set to 5%, **When** a new order group would exceed this risk threshold, **Then** the system rejects the creation with a risk limit exceeded error

---

### User Story 4 - Close Order Group with Position Unwinding (Priority: P2)

A strategy needs to close an entire order group, which involves: (1) closing any open position resulting from activated orders, and (2) cancelling any pending orders in the group.

**Why this priority**: Important for clean exit scenarios (end of day, strategy stop, manual intervention) but not required for basic operation.

**Independent Test**: Can be tested by creating an order group with a filled opening order, calling close group, and verifying the position is closed and pending orders cancelled.

**Acceptance Scenarios**:

1. **Given** an order group with filled opening order and pending closing orders, **When** the group is closed, **Then** a market order is placed to close the position and all pending closing orders are cancelled
2. **Given** an order group with only pending orders (opening not yet filled), **When** the group is closed, **Then** all pending orders are cancelled with no position impact
3. **Given** an order group with partially filled closing orders, **When** the group is closed, **Then** remaining position is closed via market order and pending orders cancelled

---

### User Story 5 - Adjust Single Order Activation Price (Priority: P2)

A strategy needs to modify the entry price of a pending order within a group (e.g., trailing the entry based on price action).

**Why this priority**: Useful for dynamic strategies but not required for core functionality.

**Independent Test**: Can be tested by adjusting the price of a pending opening order and verifying the order is modified while maintaining group integrity.

**Acceptance Scenarios**:

1. **Given** an order group with a pending opening order at price 100, **When** the activation price is adjusted to 99, **Then** the order is cancelled and resubmitted at the new price
2. **Given** an order group with an already filled order, **When** price adjustment is attempted on that order, **Then** the system returns an error indicating the order is already filled
3. **Given** an order group with pending closing orders, **When** a closing order price is adjusted, **Then** the specific closing order is modified while other orders remain unchanged

---

### User Story 6 - Market Closing Orders with Optional Protective Orders (Priority: P2)

A strategy needs to create order groups where the closing mechanism uses market orders (for guaranteed exit) but can optionally have limit protective orders as well.

**Why this priority**: Provides flexibility in exit strategies but not core to the order group concept.

**Independent Test**: Can be tested by creating an order group with market closing orders and optional limit orders, then verifying all are placed in the same group.

**Acceptance Scenarios**:

1. **Given** a signal specifying market closing orders, **When** an order group is created, **Then** the closing orders are configured as market orders
2. **Given** a signal with market closing and optional limit protective orders, **When** the group is created, **Then** all orders belong to the same group
3. **Given** market closing orders, **When** the opening order fills, **Then** market closing orders are placed immediately at market price

---

### User Story 7 - Persist Order Groups to JSON (Live Mode) (Priority: P3)

During live trading, order group state must be persisted to disk as JSON to enable recovery after restarts.

**Why this priority**: Important for production resilience but can operate without persistence during development/backtesting.

**Independent Test**: Can be tested by creating order groups, triggering persistence, restarting the system, and verifying state is restored.

**Acceptance Scenarios**:

1. **Given** active order groups during live mode, **When** state changes occur, **Then** the state is written to JSON file
2. **Given** a persisted JSON state file, **When** the system starts, **Then** order groups are restored from the file
3. **Given** backtest mode, **When** order groups are managed, **Then** no JSON persistence occurs (memory only)

---

### Edge Cases

- What happens when an opening order is cancelled externally (e.g., by exchange)? The group should transition to cancelled state, no closing orders placed.
- How does the system handle partial fills on closing orders? Track remaining volume, update group state accordingly.
- What happens when max groups limit is reached and an existing group completes? New groups can be created up to the limit.
- How does the system handle order rejection from the exchange? Mark order as rejected, provide hooks for strategy to respond.
- What happens if multiple closing orders fill simultaneously? Process each fill independently, update group state atomically.
- What if the system restarts mid-fill during live trading? Restore from persisted state and reconcile with broker state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support creating order groups with exactly one opening order and one or more closing orders
- **FR-002**: System MUST validate that total closing order volume equals opening order volume when `throwIfNotMatchingVolume` is enabled
- **FR-003**: System MUST generate default GroupId in format `{SecurityId}_{DateTimeWithMs}_{OpenPrice}` when not provided
- **FR-004**: System MUST support custom GroupId assignment during order group creation
- **FR-005**: System MUST automatically place closing orders when the opening order is filled
- **FR-006**: System MUST support proportional closing order placement for partial opening order fills using pro-rata scaling (all closing orders scaled proportionally to the fill percentage)
- **FR-007**: System MUST track multiple simultaneous order groups per security independently
- **FR-008**: System MUST enforce configurable maximum number of order groups per security
- **FR-009**: System MUST enforce configurable maximum risk percentage from current equity per order group, calculated as: (Entry Price × Volume × Stop Distance %) / Current Equity
- **FR-010**: System MUST provide state tracking for orders (Pending, Active, PartiallyFilled, Filled, Cancelled, Rejected)
- **FR-011**: System MUST provide state tracking for order groups (Pending, Active, Closing, Completed, Cancelled)
- **FR-012**: System MUST provide activation hooks when opening orders are filled
- **FR-013**: System MUST provide cancellation hooks when orders are cancelled
- **FR-014**: System MUST support closing an entire order group, unwinding any open position impact
- **FR-015**: System MUST support adjusting the activation price of pending orders within a group
- **FR-016**: System MUST automatically select appropriate order types for non-market orders based on price relationship to current market
- **FR-017**: System MUST support market closing orders with optional additional protective limit orders in the same group
- **FR-018**: System MUST persist order group state to JSON during live trading mode (Infrastructure responsibility)
- **FR-019**: System MUST restore order group state from JSON on startup in live mode (Infrastructure responsibility)
- **FR-020**: System MUST define abstractions for order group tracking in Core project
- **FR-021**: System MUST implement concrete order group tracking in Infrastructure project

### Key Entities

- **OrderGroup**: Represents a collection of related orders for a single trade concept. Contains one opening order and multiple closing orders. Has a unique GroupId, security reference, current state, and timestamp.
- **GroupedOrder**: Represents a single order within an order group. Contains order reference, volume, price, order type, role (Opening/Closing), and current state.
- **OrderGroupState**: Enumeration of possible group states (Pending, Active, Closing, Completed, Cancelled)
- **GroupedOrderState**: Enumeration of possible order states within a group (Pending, Active, PartiallyFilled, Filled, Cancelled, Rejected)
- **OrderGroupLimits**: Configuration for maximum groups per security and maximum risk percentage per group
- **ExtendedTradeSignal**: Extended version of TradeSignal that supports multiple closing orders with fractional volumes, custom GroupId, and volume matching validation flag

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Strategy developers can create order groups with multiple closing orders in a single operation
- **SC-002**: Position protection is established within one order processing cycle after opening order fill
- **SC-003**: System correctly tracks and reports state for up to 100 simultaneous order groups per security
- **SC-004**: Risk limits prevent position exposure exceeding configured thresholds
- **SC-005**: Order group closure completes all necessary operations (position close + order cancellation) in a single logical operation
- **SC-006**: Live trading sessions can be resumed after restart with full order group state restored
- **SC-007**: Backtest performance is not degraded by more than 10% compared to current single-position system when using equivalent single-group scenarios
- **SC-008**: All order group operations are auditable through state change events

## Clarifications

### Session 2025-12-11

- Q: How is "risk percentage" calculated for FR-009? → A: Risk = (Entry Price × Volume × Stop Distance %) / Current Equity
- Q: How are closing orders distributed on partial opening fill? → A: Pro-rata scaling (all closing orders scaled proportionally to fill percentage)

## Assumptions

- The existing `IStrategyOrderOperations` interface provides sufficient order management capabilities (BuyLimit, SellLimit, BuyMarket, SellMarket, CancelOrder)
- Order state transitions follow standard StockSharp patterns (Pending -> Active -> Filled/Cancelled)
- JSON serialization will use System.Text.Json per project standards
- The current equity value is available from the strategy context for risk calculations
- Order group limits are configured at strategy initialization and do not change during execution
- Market data (current price) is available for automatic order type selection
