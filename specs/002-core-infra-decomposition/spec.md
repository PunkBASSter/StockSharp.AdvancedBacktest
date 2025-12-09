# Feature Specification: Core-Infrastructure Assembly Decomposition

**Feature Branch**: `002-core-infra-decomposition`
**Created**: 2025-12-09
**Status**: Draft
**Input**: User description: "Decompose StockSharp.AdvancedBacktest into core and infrastructure assemblies where core is responsible for business logic working with contracts, while infrastructure contains implementations. Create clean, non-leaking abstractions with clear routing responsibilities."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Strategy Developer Uses Core Assembly (Priority: P1)

A strategy developer creates a new trading strategy by extending `CustomStrategyBase` from the Core assembly. They define parameters, implement trading logic using `TradeSignal` and `OrderPositionManager`, and calculate performance metrics - all without any dependency on export, debug, or storage infrastructure.

**Why this priority**: This is the primary use case for the Core assembly. Trading strategy development must work independently from infrastructure concerns to ensure clean separation and testability.

**Independent Test**: Can be fully tested by creating a strategy class that compiles and runs with only Core assembly references. The strategy can execute trading logic, manage positions, and compute metrics without infrastructure.

**Acceptance Scenarios**:

1. **Given** a new project referencing only `StockSharp.AdvancedBacktest.Core`, **When** a developer creates a class extending `CustomStrategyBase`, **Then** the project compiles successfully with access to all trading abstractions.
2. **Given** a strategy using `OrderPositionManager` and `TradeSignal`, **When** executing backtest logic without infrastructure, **Then** all order management functionality works correctly.
3. **Given** a strategy with `PerformanceMetrics` calculation, **When** metrics are computed, **Then** no infrastructure dependencies are required.

---

### User Story 2 - Infrastructure Assembly Provides Export Functionality (Priority: P1)

A backtest orchestrator uses the Infrastructure assembly to export strategy results to JSON files for web visualization. The exporter receives Core abstractions (strategies, metrics, signals) and produces output files without modifying core behavior.

**Why this priority**: Export functionality is essential for analyzing backtest results. It must consume Core types without Core knowing about export implementation details.

**Independent Test**: Can be tested by providing mock Core objects to export functions and verifying correct file output without running actual backtests.

**Acceptance Scenarios**:

1. **Given** a completed backtest result from Core, **When** `ReportBuilder` generates JSON output, **Then** files are created with correct data structure.
2. **Given** Core's `PerformanceMetrics` object, **When** passed to Infrastructure export, **Then** metrics are serialized without Core assembly changes.
3. **Given** a strategy with indicator data, **When** `IndicatorExporter` extracts values, **Then** Core strategy remains unmodified.

---

### User Story 3 - Debug Event Logging via Core Abstraction (Priority: P1)

A strategy developer enables debug logging by configuring an abstraction in Core. The actual logging implementation (SQLite, file-based) is provided by Infrastructure at runtime via dependency injection. Core code invokes logging without knowing the concrete implementation.

**Why this priority**: Debug logging is invoked from Core trading classes but implemented in Infrastructure. This requires a clean abstraction boundary to prevent implementation leakage.

**Independent Test**: Can be tested by providing a mock debug logger that implements the Core abstraction, verifying Core calls the abstraction correctly.

**Acceptance Scenarios**:

1. **Given** a Core strategy with debug logging enabled, **When** the strategy executes, **Then** logging calls are made through the abstraction interface.
2. **Given** an Infrastructure SQLite debug sink, **When** registered with the strategy, **Then** events are persisted to SQLite without Core code changes.
3. **Given** no debug implementation configured, **When** strategy runs, **Then** logging calls are no-ops without errors.

---

### User Story 4 - Optimization Runner Uses Infrastructure (Priority: P2)

A user runs strategy optimization using `OptimizerRunner` from Infrastructure. The runner creates strategy instances from Core, coordinates parallel execution, and collects results. All optimization orchestration logic resides in Infrastructure.

**Why this priority**: Optimization is an infrastructure concern that orchestrates Core strategies. Separating this allows Core to remain focused on individual strategy logic.

**Independent Test**: Can be tested by providing mock strategy factories and verifying optimization coordination works correctly.

**Acceptance Scenarios**:

1. **Given** optimization configuration, **When** `OptimizerRunner` executes, **Then** it creates Core strategy instances and collects metrics.
2. **Given** parallel workers configured, **When** optimization runs, **Then** `SharedStorageRegistry` caches data without Core awareness.
3. **Given** optimization results, **When** best strategy is selected, **Then** Core `PerformanceMetrics` is used for comparison.

---

### User Story 5 - Clean Dependency Direction (Priority: P2)

A developer examines the project references and finds Infrastructure depends on Core, but Core has zero references to Infrastructure. All abstractions in Core can be consumed without importing Infrastructure types.

**Why this priority**: Enforcing one-way dependency flow is essential for architectural integrity and prevents circular dependencies.

**Independent Test**: Can be verified by building Core assembly in isolation and checking it has no Infrastructure references.

**Acceptance Scenarios**:

1. **Given** Core assembly project file, **When** examining references, **Then** no Infrastructure assembly reference exists.
2. **Given** Infrastructure assembly project file, **When** examining references, **Then** Core assembly is referenced.
3. **Given** any Core class, **When** analyzing its imports, **Then** no Infrastructure namespace is present.

---

### Edge Cases

- What happens when debug logging abstraction is not configured in Core? Core continues execution without errors, logging calls become no-ops.
- How does system handle Infrastructure export when Core strategy has null indicator data? Export handles nulls gracefully with empty arrays or omitted fields.
- What happens when optimization attempts to use a strategy type not from Core? Compilation error due to generic constraint `where TStrategy : CustomStrategyBase`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST split current `StockSharp.AdvancedBacktest` into two assemblies: `StockSharp.AdvancedBacktest.Core` and `StockSharp.AdvancedBacktest.Infrastructure`.
- **FR-002**: Core assembly MUST contain all trading strategy abstractions: `CustomStrategyBase`, strategy modules (position sizing, stop-loss, take-profit), and factories.
- **FR-003**: Core assembly MUST contain order management logic: `IStrategyOrderOperations`, `TradeSignal`, `OrderPositionManager`.
- **FR-004**: Core assembly MUST contain parameter system: `ICustomParam`, `CustomParam<T>`, `NumberParam`, `SecurityParam`, `TimeSpanParam`, `StructParam`, `ClassParam`, `CustomParamsContainer`.
- **FR-005**: Core assembly MUST contain statistics: `IPerformanceMetricsCalculator`, `PerformanceMetrics`, `PerformanceMetricsCalculator`.
- **FR-006**: Core assembly MUST contain validation: `WalkForwardConfig`, `WalkForwardValidator`, `WalkForwardResult`, `WindowResult`, `WindowGenerationMode`.
- **FR-007**: Core assembly MUST contain models: `OptimizationConfig`, `OptimizationResult`, `GeneticConfig`, `BacktestConfig`, `BacktestResult`, `PeriodConfig`.
- **FR-008**: Core assembly MUST define debug logging abstraction (interface) that can be implemented by Infrastructure.
- **FR-009**: Infrastructure assembly MUST contain all export functionality: `Export` namespace classes.
- **FR-010**: Infrastructure assembly MUST contain all debug mode functionality: `DebugMode` namespace including `AiAgenticDebug`.
- **FR-011**: Infrastructure assembly MUST contain optimization orchestration: `Optimization` namespace classes.
- **FR-012**: Infrastructure assembly MUST contain storage caching: `Storages` namespace classes.
- **FR-013**: Infrastructure assembly MUST contain serialization utilities: `Serialization` namespace classes.
- **FR-014**: Infrastructure assembly MUST contain helper utilities: `Utilities` namespace classes.
- **FR-015**: Core assembly MUST NOT reference Infrastructure assembly (one-way dependency).
- **FR-016**: Infrastructure assembly MUST implement Core's debug logging abstraction.
- **FR-017**: System MUST maintain existing functionality - all current tests pass after decomposition.
- **FR-018**: System MUST preserve namespace structure where possible (e.g., `StockSharp.AdvancedBacktest.Strategies` stays in Core).

### Key Entities

- **Core Assembly (`StockSharp.AdvancedBacktest.Core`)**: Contains trading business logic, strategy abstractions, parameter system, order management, performance metrics, and validation logic. Depends only on StockSharp and Ecng.
- **Infrastructure Assembly (`StockSharp.AdvancedBacktest.Infrastructure`)**: Contains export, debug mode, optimization orchestration, storage caching, serialization, and utilities. Depends on Core assembly plus external packages (System.Text.Json, Microsoft.Data.Sqlite, etc.).
- **Debug Logging Abstraction**: Interface defined in Core (`IDebugEventSink` or similar) that allows strategies to emit debug events without knowing the concrete implementation.
- **Intermediate Routing Classes**: Classes in Infrastructure that bridge Core abstractions to concrete implementations, handling dependency resolution and request routing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All existing unit tests pass after assembly decomposition.
- **SC-002**: Core assembly compiles independently with zero Infrastructure references verified by examining `.csproj` references.
- **SC-003**: Strategy classes extending `CustomStrategyBase` work identically before and after decomposition.
- **SC-004**: Debug logging from Core strategies routes to Infrastructure implementations when configured.
- **SC-005**: Export functionality produces identical output before and after decomposition.
- **SC-006**: Solution builds successfully with new assembly structure.
- **SC-007**: No namespace changes break existing code in `LegacyCustomization/StrategyLauncher`.

## Assumptions

1. The existing test project (`StockSharp.AdvancedBacktest.Tests`) will be updated to reference both assemblies as needed.
2. `LegacyCustomization/StrategyLauncher` will reference both Core and Infrastructure assemblies after decomposition.
3. The debug logging abstraction will follow a simple interface pattern (e.g., `void LogEvent(...)`) without complex callback mechanisms.
4. Unused classes identified during analysis will be documented for removal in a separate iteration per user request.
5. Web visualization components (`StockSharp.AdvancedBacktest.Web`) will only need Infrastructure assembly references.

## Out of Scope

- Removing unused classes (to be handled in separate iteration after documentation).
- Creating new functionality beyond assembly decomposition.
- Modifying the `StockSharp` submodule or its integration patterns.
- Changing the public API surface of existing classes beyond namespace relocation.

## Dependencies

- StockSharp submodule must be properly initialized.
- Existing project builds and tests must pass before starting decomposition.

## Risks

- **Risk 1**: Hidden dependencies between Core and Infrastructure code discovered during separation. **Mitigation**: Careful analysis completed; any issues will require abstraction introduction.
- **Risk 2**: Namespace changes break external consumers. **Mitigation**: Preserve namespace structure and use type forwarding if needed.
- **Risk 3**: Debug logging abstraction design impacts Core simplicity. **Mitigation**: Keep abstraction minimal with optional registration pattern.
