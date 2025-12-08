<!--
  SYNC IMPACT REPORT
  Version: 1.0.0 → 1.0.0 (Ratification complete - RATIFICATION_DATE resolved)

  Modified Principles: None (ratification only)

  Added Sections: None

  Removed Sections: None

  Templates Status:
  ✅ plan-template.md - Constitution Check section validated
  ✅ spec-template.md - Requirements and user story structure compatible
  ✅ tasks-template.md - Task categorization aligns with principles
  ✅ agent-file-template.md - Generic template structure compatible
  ✅ checklist-template.md - Generic template structure compatible

  Commands Reviewed:
  ✅ speckit.plan.md - Agent-specific reference ("claude") appropriate for command context
  ✅ speckit.specify.md - No agent-specific constraints, generic guidance maintained
  ⚠️  Other speckit commands - Not reviewed in detail but assume similar pattern

  Follow-up TODOs: None
-->

# StockSharp.AdvancedBacktest Constitution

## Core Principles

### I. Separation of Concerns

Each module, class, or function MUST have a single, well-defined responsibility. Business logic (trading strategies, order management, position tracking) MUST remain isolated from infrastructure code (data export, configuration, logging, backtesting orchestration).

**Rationale**: Financial trading systems require clear boundaries between trading logic and operational concerns. This separation enables independent testing of trading algorithms, facilitates strategy reuse across different execution environments, and prevents infrastructure changes from affecting trading behavior.

**Example boundaries**:
- `CustomStrategyBase` and pluggable modules: Business logic
- `DataExporter`, `BacktestLauncher`, `DebugMode` namespace: Infrastructure

### II. Test-First Development (NON-NEGOTIABLE)

All new features MUST follow test-driven development:
1. Tests written FIRST and MUST fail initially
2. Tests reviewed and approved
3. Implementation proceeds only after test approval
4. Red-Green-Refactor cycle strictly enforced

**Rationale**: Financial backtesting requires absolute confidence in correctness. Test-first development ensures specifications are clear before implementation, prevents regression in strategy behavior, and provides executable documentation of expected behavior. Given the financial impact of bugs, testing discipline is non-negotiable.

**Testing framework**: xUnit v3 with Microsoft.NET.Test.Sdk targeting .NET 10

### III. Financial Precision

All financial calculations MUST use `decimal` type. JSON serialization MUST use custom decimal converters to prevent precision loss. Floating-point types (`float`, `double`) are PROHIBITED for monetary amounts, prices, positions, or performance metrics.

**Rationale**: Trading strategies depend on precise calculations. Floating-point rounding errors can compound over thousands of trades, leading to incorrect backtesting results and potentially catastrophic losses in live trading. System.Text.Json's default decimal handling loses precision; custom converters are mandatory.

**Implementation**: Use System.Text.Json with custom decimal converters configured in `JsonSerializerOptions`

### IV. Composition Over Inheritance

Favor composing objects with specific behaviors over creating deep inheritance hierarchies. Use dependency injection for external dependencies to enable testability and flexibility.

**Rationale**: Trading strategies require flexible combination of indicators, entry/exit rules, and risk management modules. Composition allows dynamic reconfiguration during optimization without class explosion. Inheritance hierarchies become brittle when strategies need multiple orthogonal behaviors (e.g., different position sizing + different exit rules).

**Application**: Strategy modules must be pluggable components rather than subclasses

### V. Explicit Visibility

All classes and members MUST have explicit access modifiers. No implicit `internal` or default visibility. Code should be self-documenting through clear naming; XML comments are PROHIBITED except for public APIs.

**Rationale**: Financial code is read far more often than written during debugging and optimization. Explicit modifiers clarify design intent. Self-documenting names prevent documentation drift. Complex trading logic warrants inline comments explaining "why", but simple code needs no comments explaining "what".

**Style**: Follow existing StockSharp.AdvancedBacktest conventions (prevail over StockSharp guidelines)

### VI. System.Text.Json Standard

System.Text.Json with source generation MUST be used for all new implementations. Newtonsoft.Json is acceptable ONLY for reverse compatibility with StockSharp dependencies or legacy systems.

**Rationale**: System.Text.Json offers superior performance for optimization results serialization (critical when processing thousands of strategy permutations). Source generation eliminates reflection overhead. Newtonsoft.Json introduces unnecessary dependency and slower serialization but is required where StockSharp integration demands it.

**Migration policy**: When refactoring existing code, evaluate migrating from Newtonsoft.Json unless constrained by external dependencies

### VII. End-to-End Testability

Every feature MUST be testable end-to-end in isolation. Strategies must support backtesting with mock data, output validation, and edge case simulation without requiring live market connections or external services.

**Rationale**: Backtesting strategies need reproducible test environments. Live data dependencies make tests non-deterministic and slow. Mock data enables edge case testing (market gaps, extreme volatility, order rejections) that would be impractical to capture from live markets. Isolation ensures test suite remains fast and reliable.

**Requirement**: Design all components assuming they will be tested with synthetic data

## Testing Standards

### Test Organization

- **Unit tests**: Isolated component behavior, fast execution
- **Integration tests**: Component interaction, StockSharp integration, backtesting pipeline
- **Contract tests**: Strategy parameter contracts, optimization configuration validation

### Test Requirements

- All tests MUST be repeatable and deterministic
- Financial calculations MUST be validated against known benchmarks
- Performance metrics (Sharpe ratio, drawdown, etc.) MUST match manual calculations
- Tests MUST NOT depend on external market data or network connectivity
- Test data paths MUST be configurable (avoid hardcoded paths)

### Test Coverage Expectations

- New features: Tests written before implementation (TDD)
- Bug fixes: Regression test demonstrating bug before fix
- Optimizations: Performance benchmarks to validate improvements
- Refactoring: Existing tests must continue passing

## Development Workflow

### Planning Requirements

- Understand goal, requirements, and constraints before coding
- Search codebase for existing implementations before writing new code
- Plan approach: break down tasks, consider edge cases and error handling
- Prefer refactoring/extending existing code over duplicating functionality

### Design Requirements

- APIs and interfaces MUST prioritize simplicity and clear focus
- Implementations MUST follow Liskov Substitution Principle
- Sibling classes MUST be interchangeable from consumer perspective
- No abstraction leakage from implementation details

### Code Quality Gates

- Expression-bodied members preferred for simple getters/setters/methods
- Meaningful names for variables, functions, classes (self-documenting)
- Comments ONLY for complex business logic or non-obvious implementations
- Prefer global exception handling over local try-catch blocks unless specific recovery needed

### Refactoring Triggers

When a class has too many responsibilities and dependencies, refactor into smaller, focused classes or modules. Signs include:
- More than 5 constructor dependencies
- Methods not using instance state
- Mixed concerns (e.g., trading logic + data persistence)

## Governance

### Amendment Process

1. Propose constitution changes via pull request to `.specify/memory/constitution.md`
2. Document rationale and impact on dependent templates
3. Update version according to semantic versioning:
   - **MAJOR**: Backward incompatible governance/principle removals or redefinitions
   - **MINOR**: New principle/section added or materially expanded guidance
   - **PATCH**: Clarifications, wording, typo fixes, non-semantic refinements
4. Propagate changes to dependent templates (`plan-template.md`, `spec-template.md`, `tasks-template.md`)
5. Update `LAST_AMENDED_DATE` to amendment date
6. Require stakeholder approval before merging

### Compliance Requirements

- All PRs MUST be reviewed for constitution compliance
- Violations (complexity, inheritance depth, missing tests) MUST be justified in `plan.md` Complexity Tracking section
- Unjustified violations block PR approval
- Constitution supersedes all other development practices and guidelines

### Review Focus Areas

- Separation of concerns: Business logic vs infrastructure
- Test coverage and TDD compliance
- Financial precision (decimal usage, JSON converters)
- Composition patterns vs inheritance
- API simplicity and clarity

### Runtime Development Guidance

For AI-assisted development, consult `.claude/CLAUDE.md` and `CLAUDE.md` for:
- Build commands and testing procedures
- Project architecture and component relationships
- Code style and formatting conventions
- StockSharp integration patterns

**Version**: 1.0.0 | **Ratified**: 2025-11-11 | **Last Amended**: 2025-11-11
