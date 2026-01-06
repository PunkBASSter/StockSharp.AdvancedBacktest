# Research: DeltaZz Peak/Trough Breakout Strategy

**Date**: 2025-12-25
**Status**: Complete

## Research Tasks

### 1. Signal Deduplication Pattern

**Question**: How should signal deduplication work with (Entry, SL, TP) tuple keys?

**Decision**: Implement a `SignalDeduplicator` class that tracks the last generated signal using a record-based key. Reset state when position closes.

**Rationale**:
- Peak/Trough indicators emit values that persist across multiple candles (unlike combined ZigZag which alternates)
- Simple record-based key comparison with decimal equality is sufficient (exact price matching, no tolerance needed)
- Reset on position close allows same setup to trigger again after trade completes

**Alternatives Considered**:
1. **HashSet of all historical signals**: Rejected - would prevent re-entry on same levels after position close
2. **Time-based expiration**: Rejected - adds complexity, position close is the natural reset point
3. **Price tolerance matching**: Rejected - trading prices are exact decimals, no floating-point concerns

**Implementation Notes**:
```csharp
public record SignalKey(decimal EntryPrice, decimal StopLoss, decimal TakeProfit);

public class SignalDeduplicator
{
    private SignalKey? _lastSignal;

    public bool IsDuplicate(decimal entry, decimal sl, decimal tp)
    {
        var key = new SignalKey(entry, sl, tp);
        if (_lastSignal == key) return true;
        _lastSignal = key;
        return false;
    }

    public void Reset() => _lastSignal = null;
}
```

---

### 2. Peak/Trough History Combination

**Question**: How to combine separate Peak and Trough indicator values into a unified zigzag pattern for signal detection?

**Decision**: Maintain a single chronologically-ordered list of non-empty indicator values from both Peak and Trough indicators, storing both value and direction (peak=up, trough=down).

**Rationale**:
- The original ZigZagBreakoutStrategy uses `_dzzHistory` which stores all non-empty ZigZag values
- With separate Peak/Trough indicators, we need to merge their outputs chronologically
- Each indicator emits at most one value per candle (they never overlap on same timestamp)
- Pattern matching logic needs the last 3 non-zero points regardless of peak/trough type

**Alternatives Considered**:
1. **Separate peak and trough lists**: Rejected - pattern matching needs unified chronological sequence
2. **Use underlying DeltaZigZag directly**: Rejected - defeats purpose of separate indicators for visualization
3. **Store raw indicator values with timestamp lookup**: Rejected - over-engineered for simple pattern matching

**Implementation Notes**:
- Add values to history when either indicator emits a non-empty value
- Pattern extraction is identical to original: `TakeLast(3)` of non-zero values

---

### 3. Launcher Abstraction Pattern

**Question**: What interface should launchers implement for DI integration?

**Decision**: Define `IStrategyLauncher` interface with async `RunAsync` method that returns a result code.

**Rationale**:
- Async pattern matches existing `BacktestRunner.RunAsync()` usage
- Result code (int) aligns with Program.cs return value for CLI
- Interface allows DI container to resolve different launchers polymorphically
- Simple contract: configure and run, return success/failure

**Alternatives Considered**:
1. **Abstract base class**: Rejected - launchers may have different dependencies, interface is more flexible
2. **Multiple methods (Configure, Run, Report)**: Rejected - over-separation, launchers encapsulate full workflow
3. **Generic interface with strategy type**: Rejected - adds complexity, simple interface sufficient

**Implementation Notes**:
```csharp
public interface IStrategyLauncher
{
    string Name { get; }
    Task<int> RunAsync(bool aiDebug);
}
```

---

### 4. DI Container Integration Scope

**Question**: Should ZigZagBreakoutLauncher also get full DI integration, or just the new DzzPeakTroughLauncher?

**Decision**: Both launchers should use DI for consistency, but the initial refactoring can be incremental - extract ZigZagBreakoutLauncher first, then add DI to both.

**Rationale**:
- FR-014 states "Launchers MUST register strategy dependencies in the DI container"
- Consistency is valuable for maintainability
- Incremental approach reduces risk: first verify extraction produces identical results, then add DI

**Implementation Notes**:
- Phase 1: Extract ZigZagBreakoutLauncher with minimal changes (verify identical behavior)
- Phase 2: Add IStrategyLauncher interface
- Phase 3: Add DI container registration for both launchers
- Both launchers register same core services (BacktestRunner, PerformanceMetricsCalculator, etc.)

---

### 5. Strategy Selection Mechanism

**Question**: How should users select which strategy/launcher to run?

**Decision**: Use command-line `--strategy` argument with default to ZigZagBreakout for backward compatibility.

**Rationale**:
- Existing usage (`dotnet run`) should continue working without changes
- Simple string-based selection is sufficient for 2 strategies
- DI container can resolve the appropriate launcher based on selection

**Alternatives Considered**:
1. **Separate executables per strategy**: Rejected - duplication, harder to maintain
2. **Configuration file**: Rejected - command-line is simpler for single-run backtests
3. **Environment variable**: Rejected - command-line is more explicit and discoverable

**Implementation Notes**:
```csharp
var strategyOption = new Option<string>(
    name: "--strategy",
    description: "Strategy to run (ZigZagBreakout, DzzPeakTrough)",
    getDefaultValue: () => "ZigZagBreakout");
```

---

### 6. Existing Code Patterns

**Question**: What patterns does the existing codebase use that we should follow?

**Decision**: Follow established patterns from ZigZagBreakoutStrategy and OrderPositionManager.

**Findings**:
- **Strategy Configuration**: Use `*Config` record class with parameters (see `ZigZagBreakoutConfig`)
- **Indicator Registration**: Add to `Indicators.Add()` in `OnStarted2()` before calling `base.OnStarted2()`
- **Order Management**: Use existing `OrderPositionManager` with `HandleOrderRequest()` and `CheckProtectionLevels()`
- **Position Sizing**: Use `IRiskAwarePositionSizer` interface (e.g., `FixedRiskPositionSizer`)
- **Candle Subscription**: Use `SubscribeCandles(subscription).BindWithEmpty(indicator, callback).Start()`

**Implementation Notes**:
- Strategy inherits from `CustomStrategyBase`
- Config follows pattern: parameters with sensible defaults
- Debug logging via `LogDebug()` and `LogInfo()` extension methods

---

## Summary

All research questions resolved. No blocking unknowns remain.

| Topic | Decision | Risk Level |
|-------|----------|------------|
| Signal Deduplication | Record-based key, reset on position close | Low |
| History Combination | Unified chronological list | Low |
| Launcher Abstraction | IStrategyLauncher interface | Low |
| DI Scope | Both launchers, incremental rollout | Low |
| Strategy Selection | --strategy CLI argument | Low |
| Code Patterns | Follow existing ZigZagBreakout patterns | Low |
