# StockSharp Advanced Backtesting Library - Technical Requirements Document (TRD)

**Version**: 2.0 (Pragmatic Edition)
**Date**: 2025-10-04
**Status**: Ready for Implementation
**Target Framework**: .NET 10

---

## Executive Summary

### Reality Check

The CustomizationPoC/StrategyLauncher is **98% complete**. This TRD documents the actual work needed to:

1. **Copy working PoC code** to library structure (mostly renames)
2. **Add multi-window walk-forward validation** (extending existing train/validate split)
3. **Polish and ship**

**Timeline**: 1.5-2 weeks (10 working days)
**Approach**: Pragmatic migration, not architectural perfection

### Key Facts from Code Analysis

- ✅ **System.Text.Json already implemented** - ReportBuilder uses it, no migration needed
- ✅ **Train/validate split already works** - CustomOptimizer runs both training and validation periods
- ⚠️ **Multi-window walk-forward needed** - Extend single-window to 3+ windows with efficiency metrics

### What We're NOT Doing

- ❌ Creating wrapper classes around wrappers
- ❌ Building abstract interfaces for single implementations
- ❌ Over-engineering parameter generators that already work
- ❌ Rewriting StockSharp's BruteForceOptimizer
- ❌ Creating "orchestrators" for simple fluent APIs

### What We ARE Doing

- ✅ Copying PoC files to library with namespace updates
- ✅ Adding multi-window WalkForwardValidator (extending existing validation)
- ✅ Writing tests for critical paths
- ✅ Creating documentation and examples

**Note**: System.Text.Json is already in use in the PoC, no migration needed.

---

## Architecture: Current vs. Target

### Current PoC Architecture (What Works)

```
OptimizationLauncher<TStrategy>
├── CustomOptimizer<TStrategy>
│   ├── BruteForceOptimizer (StockSharp)
│   ├── Parameter generation (Cartesian product)
│   └── Strategy instance creation
├── PerformanceAnalyzer
│   └── Metrics calculation (Sharpe, Sortino, etc.)
└── ReportBuilder
    ├── Chart data extraction
    └── HTML report generation
```

**Key Files**:

- `OptimizationLauncher.cs` - Fluent API entry point ✅
- `CustomOptimizer.cs` - Wraps BruteForceOptimizer ✅
- `PerformanceAnalyzer.cs` - Metrics calculation ✅
- `ReportBuilder.cs` - HTML/JSON export ✅
- `CustomStrategyBase.cs` - Strategy base class ✅
- `CustomParams/*.cs` - Parameter system ✅

### Target Library Architecture (Minimal Changes)

```
StockSharp.AdvancedBacktest/
├── Optimization/
│   ├── OptimizationLauncher.cs (copy from PoC, rename namespace)
│   └── OptimizerRunner.cs (rename from CustomOptimizer)
├── Parameters/
│   └── *.cs (copy all CustomParams, update namespace)
├── Statistics/
│   └── MetricsCalculator.cs (rename from PerformanceAnalyzer)
├── Validation/
│   └── WalkForwardValidator.cs (NEW - only new component)
├── Export/
│   └── ReportBuilder.cs (already uses System.Text.Json, add async)
└── Strategies/
    └── CustomStrategyBase.cs (copy as-is)
```

### Migration Map (File by File)

| PoC File | Library File | Changes Required |
|----------|-------------|------------------|
| `LauncherBase.cs` | `Optimization/LauncherBase.cs` | Namespace only |
| `OptimizationLauncher.cs` | `Optimization/OptimizationLauncher.cs` | Namespace + add .WithWalkForward() |
| `CustomOptimizer.cs` | `Optimization/OptimizerRunner.cs` | Rename class, keep logic |
| `ICustomOptimizer.cs` | (delete) | Not needed |
| `PerformanceAnalyzer.cs` | `Statistics/MetricsCalculator.cs` | Rename, keep all logic |
| `PerformanceMetrics.cs` | `Statistics/PerformanceMetrics.cs` | Copy as-is |
| `OptimizationResult.cs` | `Models/OptimizationResult.cs` | Copy as-is |
| `CustomParams/*.cs` | `Parameters/*.cs` | Namespace only |
| `CustomStrategyBase.cs` | `Strategies/CustomStrategyBase.cs` | Copy as-is |
| `ReportBuilder.cs` | `Export/ReportBuilder.cs` | Add async (already uses System.Text.Json) |
| `ChartDataModels.cs` | `Export/ChartDataModels.cs` | Copy as-is |
| (none) | `Validation/WalkForwardValidator.cs` | NEW |

---

## Component Specifications

### Only Document What Changes

#### 1. WalkForwardValidator (NEW Component)

**Purpose**: The ONE new component we're actually building

**Namespace**: `StockSharp.AdvancedBacktest.Validation`

**Implementation**:

```csharp
namespace StockSharp.AdvancedBacktest.PerformanceValidation;

public class WalkForwardValidator
{
    public TimeSpan WindowSize { get; set; }
    public TimeSpan StepSize { get; set; }
    public TimeSpan ValidationSize { get; set; }

    public WalkForwardResult Validate<TStrategy>(
        TStrategy strategy,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        OptimizerRunner<TStrategy> optimizerRunner)
        where TStrategy : CustomStrategyBase, new()
    {
        var windows = GenerateWindows(startDate, endDate);
        var results = new List<WindowResult>();

        foreach (var window in windows)
        {
            // Re-optimize on training period
            var trainResults = optimizerRunner.OptimizeWindow(
                window.TrainStart, window.TrainEnd, strategy.ParamsBackup);

            // Test on validation period
            var testResults = optimizerRunner.TestWindow(
                window.TestStart, window.TestEnd, trainResults.BestParams);

            results.Add(new WindowResult
            {
                TrainingMetrics = trainResults.Metrics,
                TestingMetrics = testResults.Metrics,
                PerformanceDegradation = CalculateDegradation(trainResults, testResults)
            });
        }

        return new WalkForwardResult
        {
            TotalWindows = windows.Count,
            Windows = results,
            WalkForwardEfficiency = CalculateWFEfficiency(results)
        };
    }
}
```

**Integration with Existing Code**:

```csharp
// Add to OptimizationLauncher.cs
public OptimizationLauncher<TStrategy> WithWalkForward(WalkForwardValidator validator)
{
    _walkForwardValidator = validator;
    return this;
}
```

#### 2. System.Text.Json Migration

**File**: `Export/ReportBuilder.cs`

**Changes**:

- Replace `Newtonsoft.Json` → `System.Text.Json`
- Add async methods (no functional changes)
- Add decimal precision converter

**Before (Newtonsoft)**:

```csharp
using Newtonsoft.Json;

public void ExportToJson(object data, string path)
{
    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
    File.WriteAllText(path, json);
}
```

**After (System.Text.Json)**:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

private static readonly JsonSerializerOptions _jsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new DecimalStringConverter() }
};

public async Task ExportToJsonAsync(object data, string path, CancellationToken ct = default)
{
    using var stream = File.Create(path);
    await JsonSerializer.SerializeAsync(stream, data, _jsonOptions, ct);
}
```

**Decimal Converter** (prevent precision loss):

```csharp
public class DecimalStringConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => decimal.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("G29", CultureInfo.InvariantCulture));
}
```

#### 3. Namespace Reorganization

**Old (PoC)**:

```csharp
namespace StockSharp.StrategyLauncher;
namespace StockSharp.Samples.MaCrossoverBacktester;
```

**New (Library)**:

```csharp
namespace StockSharp.AdvancedBacktest.Optimization;
namespace StockSharp.AdvancedBacktest.Parameters;
namespace StockSharp.AdvancedBacktest.Statistics;
namespace StockSharp.AdvancedBacktest.PerformanceValidation;
namespace StockSharp.AdvancedBacktest.Export;
namespace StockSharp.AdvancedBacktest.Strategies;
```

---

## Implementation Timeline (1.5-2 Weeks / 10 Days)

### Phase 1: Library Structure & Migration (Days 1-3)

**Objective**: Get PoC code working in library structure

**Day 1-2: Project Setup**

- ✅ Create `StockSharp.AdvancedBacktest` .NET 10 class library
- ✅ Copy all PoC files to appropriate library folders
- ✅ Update all namespaces to `StockSharp.AdvancedBacktest.*`
- ✅ Rename classes (CustomOptimizer → OptimizerRunner, etc.)
- ✅ Fix references and ensure project compiles

**Day 3: Testing & Validation**

- ✅ Port existing PoC tests to library test project
- ✅ Add unit tests for MetricsCalculator
- ✅ Add unit tests for parameter generation
- ✅ Verify System.Text.Json export works correctly
- ✅ Ensure all existing functionality works

**Deliverable**: Library compiles, existing PoC features work, basic tests pass

---

### Phase 2: Multi-Window Walk-Forward Validator (Days 4-7)

**Objective**: Extend existing validation to multi-window walk-forward

**Background**: CustomOptimizer already performs single train/validate split. Need to extend to multiple windows.

**Day 4: Design & Models**

- ✅ Create `WalkForwardConfig` model
- ✅ Create `WalkForwardResult` and `WindowResult` models
- ✅ Design window generation algorithm (anchored/rolling)

**Day 5-6: WalkForwardValidator Implementation**

- ✅ Create `WalkForwardValidator` class
- ✅ Implement time window generation (anchored/rolling)
- ✅ Refactor OptimizerRunner to support multiple window execution
- ✅ Add re-optimization logic for each training window
- ✅ Calculate WF Efficiency: `avgOOS / avgIS`
- ✅ Calculate consistency score across windows

**Day 7: Integration & Testing**

- ✅ Add `.WithWalkForward()` method to OptimizationLauncher
- ✅ Integrate WF results into OptimizationResult
- ✅ Update JSON export to include WF metrics
- ✅ Test 3-fold walk-forward on sample data
- ✅ Verify WF efficiency calculation

**Deliverable**: Multi-window walk-forward validation works, integrated with launcher, tested

---

### Phase 3: Documentation & Ship (Days 8-10)

**Objective**: Polish, document, and prepare for release

**Day 8: Documentation**

- ✅ README with quick start guide
- ✅ API documentation (XML comments on all public APIs)
- ✅ Walk-forward guide explaining concepts and metrics
- ✅ Migration guide from PoC

**Day 9: Examples & Web Integration**

- ✅ Simple MA crossover example
- ✅ Walk-forward validation example
- ✅ Document Web template integration (static HTML copying)
- ✅ JSON schema documentation

**Day 10: Final Polish & Ship**

- ✅ Performance testing: 1000 param combinations < 5 min
- ✅ NuGet package configuration
- ✅ Final code review
- ✅ Tag v1.0.0 release

**Deliverable**: NuGet package ready, documentation complete, examples work

---

## Acceptance Criteria

### Phase 1 Complete ✓ (Day 3)

- [ ] All PoC files copied to library structure
- [ ] Namespaces updated to `StockSharp.AdvancedBacktest.*`
- [ ] Classes renamed (CustomOptimizer → OptimizerRunner, PerformanceAnalyzer → MetricsCalculator)
- [ ] Verified ReportBuilder uses System.Text.Json (no migration needed)
- [ ] 10+ unit tests passing (parameter generation, metrics calculation, JSON export)
- [ ] Library project compiles without errors
- [ ] Existing train/validate split functionality preserved

### Phase 2 Complete ✓ (Day 7)

- [ ] WalkForwardValidator class implemented
- [ ] Window generation works (anchored & rolling modes)
- [ ] Multi-window optimization executes successfully (3+ windows)
- [ ] OptimizationLauncher has `.WithWalkForward(config)` method
- [ ] WF efficiency calculation correct: `avgOOS / avgIS`
- [ ] Consistency score calculation working
- [ ] Validation results export to JSON with WF metrics
- [ ] Integration test: full pipeline with 3-fold walk-forward passes
- [ ] WF efficiency calculation verified against manual calculation

### Phase 3 Complete ✓ (Day 10)

- [ ] README with quick start (< 5 min to first run)
- [ ] Walk-forward guide complete (concepts, metrics, best practices)
- [ ] 3+ usage examples documented
- [ ] Web template integration documented
- [ ] XML doc comments on all public APIs
- [ ] NuGet package builds successfully
- [ ] Performance: 1000 param combinations complete in < 5 min

---

## Explicit Scope Cuts

### What the Original TRD Proposed (That We're NOT Doing)

1. ❌ **BruteForceOptimizerWrapper** - CustomOptimizer already wraps it, no need for wrapper inception
2. ❌ **IOptimizationOrchestrator interface** - YAGNI, we have one implementation
3. ❌ **ParameterGenerator class** - Methods in OptimizerRunner work fine
4. ❌ **IValidationStrategy interface** - Single implementation, no polymorphism needed
5. ❌ **ValidationPipeline class** - Over-engineered, just call validator directly
6. ❌ **Monte Carlo validator** - Stretch goal, not MVP
7. ❌ **Out-of-sample validator** - Walk-forward covers it
8. ❌ **Multiple export formats** - JSON is enough for MVP
9. ❌ **Backward compatibility layer** - No existing users to support
10. ❌ **Source generation for JSON** - Overkill for MVP, reflection is fine

### Why These Cuts Make Sense

**YAGNI Principle**: You Aren't Gonna Need It

- Don't build abstractions until you have 2+ implementations
- Don't create interfaces for mocking - use concrete classes in tests
- Don't over-engineer for "future flexibility"

**Ship Working Code**:

- PoC works → Copy it → Ship it
- Add walk-forward (the ONE missing piece)
- Iterate based on real usage, not hypothetical scenarios

---

## Risk Assessment & Mitigation

### Risk 1: Scope Creep During Week 1

**Symptom**: "While we're here, let's refactor..."

**Impact**: Week 1 bleeds into Week 2, delays everything

**Mitigation**:

- Strict rule: NO refactoring beyond renames and namespace updates
- Code review checklist: "Is this a copy or a rewrite?"
- If it's working in PoC, don't touch the logic

### Risk 2: Walk-Forward Complexity

**Symptom**: "We need configurable window strategies, multiple validation modes..."

**Impact**: Week 2 turns into month-long validation framework project

**Mitigation**:

- Start with simple 2-fold split (train/test)
- Hard-code anchored window strategy first
- Add rolling windows only if time permits
- Defer Monte Carlo to v1.1

### Risk 3: Perfect Architecture Syndrome

**Symptom**: "This design isn't SOLID/DRY/KISS enough..."

**Impact**: Endless refactoring, never ship

**Mitigation**:

- Working code > clean code for MVP
- Ship v1.0, refactor in v1.1 with real user feedback
- Prefer duplication over wrong abstraction
- Remember: PoC is 95% done, just need to package it

### Risk 4: JSON Export Compatibility

**Symptom**: "JSON output doesn't match PoC format..."

**Impact**: Breaking changes for users importing JSON

**Mitigation**:

- PoC already uses System.Text.Json, so format is consistent
- Keep same JSON schema (property names, structure)
- Verify decimal converter is in place for financial precision
- Test roundtrip: PoC JSON → Library → JSON (should match)

---

## API Examples

### Basic Optimization (Same as PoC)

```csharp
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Parameters;

var results = new OptimizationLauncher<MaCrossoverStrategy>(
        trainingPeriod,
        new OptimizerRunner<MaCrossoverStrategy>())
    .WithStrategyParams(
        new NumberParam("FastPeriod", 5, 20, 5),
        new NumberParam("SlowPeriod", 30, 60, 10),
        new SecurityParam("AAPL@NASDAQ", TimeFrame.Minute))
    .WithParamValidation(p =>
        p["FastPeriod"].GetValue<int>() < p["SlowPeriod"].GetValue<int>())
    .WithMetricsFilter(m => m.SharpeRatio > 1.0)
    .WithMetricsFilter(m => m.MaxDrawdown < 15.0)
    .Launch();

Console.WriteLine($"Best strategy: Sortino {results.First().Value.TrainingMetrics.SortinoRatio:F2}");
```

### Walk-Forward Validation (NEW)

```csharp
var results = new OptimizationLauncher<MaCrossoverStrategy>(
        trainingPeriod,
        new OptimizerRunner<MaCrossoverStrategy>())
    .WithStrategyParams(/* ... */)
    .WithWalkForward(new WalkForwardValidator
    {
        WindowSize = TimeSpan.FromDays(90),      // 3-month training
        StepSize = TimeSpan.FromDays(30),        // 1-month steps
        ValidationSize = TimeSpan.FromDays(30)   // 1-month test
    })
    .WithMetricsFilter(m => m.WalkForwardEfficiency > 0.5)
    .Launch();

var best = results.OrderByDescending(r => r.Value.WalkForwardResult.WalkForwardEfficiency).First();
Console.WriteLine($"WF Efficiency: {best.Value.WalkForwardResult.WalkForwardEfficiency:F2}");
```

---

## Testing Strategy

### Unit Tests (Focused)

**Parameter Generation**:

```csharp
[Fact]
public void CartesianProduct_ThreeParams_GeneratesCorrectCount()
{
    var params = new[]
    {
        new NumberParam("A", 1, 3, 1),    // 3 values
        new NumberParam("B", 10, 20, 5),  // 3 values
        new SecurityParam("AAPL@NASDAQ", TimeFrame.Minute) // 1 value
    };

    var combinations = /* generate */;

    Assert.Equal(9, combinations.Count); // 3 * 3 * 1 = 9
}
```

**Metrics Calculation**:

```csharp
[Theory]
[InlineData(10.0, 5.0, 2.0)] // 10% return, 5% stddev → 2.0 Sharpe
public void SharpeRatio_Calculation_MatchesExpected(double returns, double stdDev, double expected)
{
    var metrics = MetricsCalculator.Calculate(/* ... */);
    Assert.InRange(metrics.SharpeRatio, expected - 0.01, expected + 0.01);
}
```

**JSON Serialization**:

```csharp
[Fact]
public void JsonRoundtrip_PreservesDecimalPrecision()
{
    var original = new PerformanceMetrics { InitialCapital = 100000.12345678m };
    var json = JsonSerializer.Serialize(original, _options);
    var deserialized = JsonSerializer.Deserialize<PerformanceMetrics>(json, _options);

    Assert.Equal(original.InitialCapital, deserialized.InitialCapital);
}
```

### Integration Tests (End-to-End)

**Full Pipeline Test**:

```csharp
[Fact]
public void FullOptimization_MACrossover_CompletesSuccessfully()
{
    var launcher = new OptimizationLauncher<MaCrossoverStrategy>(
        trainingPeriod, new OptimizerRunner<MaCrossoverStrategy>())
        .WithStrategyParams(/* ... */)
        .WithMetricsFilter(m => m.TotalTrades >= 10);

    var results = launcher.Launch();

    Assert.NotEmpty(results);
    Assert.All(results.Values, r => Assert.True(r.TrainingMetrics.TotalTrades >= 10));
}
```

**Walk-Forward Test**:

```csharp
[Fact]
public void WalkForward_ThreeWindows_CalculatesEfficiency()
{
    var launcher = /* ... */
        .WithWalkForward(new WalkForwardValidator
        {
            WindowSize = TimeSpan.FromDays(60),
            StepSize = TimeSpan.FromDays(30),
            ValidationSize = TimeSpan.FromDays(30)
        });

    var results = launcher.Launch();
    var wfResult = results.First().Value.WalkForwardResult;

    Assert.True(wfResult.TotalWindows >= 3);
    Assert.InRange(wfResult.WalkForwardEfficiency, 0.0, 2.0);
}
```

---

## Migration Guide (PoC → Library)

### For Existing PoC Users

**Step 1: Update Package Reference**

```xml
<!-- Remove PoC project reference -->
<!-- <ProjectReference Include="..\CustomizationPoC\StrategyLauncher\StrategyLauncher.csproj" /> -->

<!-- Add library NuGet package -->
<PackageReference Include="StockSharp.AdvancedBacktest" Version="1.0.0" />
```

**Step 2: Update Namespaces**

```csharp
// OLD
using StockSharp.StrategyLauncher;
using StockSharp.Samples.MaCrossoverBacktester;

// NEW
using StockSharp.AdvancedBacktest.Optimization;
using StockSharp.AdvancedBacktest.Parameters;
using StockSharp.AdvancedBacktest.Statistics;
```

**Step 3: Rename Classes (if needed)**

```csharp
// OLD
var optimizer = new CustomOptimizer<MyStrategy>();

// NEW
var optimizer = new OptimizerRunner<MyStrategy>();
```

**Step 4: Code Works As-Is**

The API is **99% compatible**. Your existing code should work with minimal changes:

- Same fluent API (`.WithStrategyParams()`, `.WithMetricsFilter()`, etc.)
- Same parameter types (`NumberParam`, `SecurityParam`, etc.)
- Same strategy base class (`CustomStrategyBase`)
- Same result structure (`OptimizationResult`, `PerformanceMetrics`)

---

## NuGet Package Structure

### Package Metadata

```xml
<PropertyGroup>
  <PackageId>StockSharp.AdvancedBacktest</PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Team</Authors>
  <Description>
    Advanced backtesting library extending StockSharp with walk-forward validation,
    comprehensive metrics, and professional reporting. Built on proven PoC codebase.
  </Description>
  <PackageTags>stocksharp;backtesting;trading;optimization;validation</PackageTags>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Dependencies

```xml
<ItemGroup>
  <!-- StockSharp (via symbolic link in dev, NuGet in package) -->
  <PackageReference Include="StockSharp.Algo" Version="5.0.*" />
  <PackageReference Include="StockSharp.BusinessEntities" Version="5.0.*" />

  <!-- System.Text.Json -->
  <PackageReference Include="System.Text.Json" Version="9.0.*" />
</ItemGroup>
```

---

## Success Metrics

### Development Velocity

- [ ] Week 1: Library compiles with all PoC features
- [ ] Week 2: Walk-forward validation integrated
- [ ] Week 3: NuGet package published

### Code Quality

- [ ] 80%+ test coverage on critical paths
- [ ] All public APIs have XML documentation
- [ ] Zero P0 bugs in walk-forward logic

### Performance

- [ ] 1000 parameter combinations: < 5 minutes
- [ ] JSON export (10K strategies): < 5 seconds
- [ ] HTML report generation: < 2 seconds
- [ ] Memory: < 2GB for typical optimization

### User Experience

- [ ] Developer can run first optimization in < 5 minutes
- [ ] Migration from PoC requires < 10 lines of code changes
- [ ] Walk-forward validation adds < 5 lines to launcher config

---

## Appendix: Design Decisions

### Decision 1: No Wrapper Inception

**Question**: Should we wrap CustomOptimizer in another wrapper?

**Answer**: NO. CustomOptimizer already wraps BruteForceOptimizer. That's enough.

**Rationale**:

- PoC has: `CustomOptimizer` wraps `BruteForceOptimizer` ✅
- TRD v1 proposed: `BruteForceOptimizerWrapper` wraps `CustomOptimizer` wraps `BruteForceOptimizer` ❌
- This is wrapper inception - adds zero value, only complexity

### Decision 2: No Premature Interfaces

**Question**: Should we create `IValidationStrategy` interface?

**Answer**: NO. Not until we have 2+ implementations OR need DI.

**Rationale**:

- YAGNI: Interfaces are for polymorphism, we have one validator
- DI Exception: Create interfaces if components will be injected via DI container
- Testability: Concrete `WalkForwardValidator` is easy to test without interface
- Flexibility: Can add interface later when we add Monte Carlo validator or need DI

**Rule of Thumb**:

- Single implementation + no DI → No interface (concrete class is fine)
- Single implementation + DI → Interface needed (for container registration)
- Multiple implementations → Interface needed (regardless of DI)

### Decision 3: System.Text.Json vs. Newtonsoft

**Question**: Is System.Text.Json migration worth it?

**Answer**: YES. Modern .NET, better performance, no external deps.

**Migration**:

- Add `DecimalStringConverter` for precision
- Use `[JsonPropertyName]` for compatibility
- Async methods for better scalability
- ~2 hours of work for long-term benefits

### Decision 4: 3-Week Timeline

**Question**: Can we really ship in 3 weeks?

**Answer**: YES. PoC is 95% done, we're just packaging it.

**Breakdown**:

- Week 1: Copy & paste (not rocket science)
- Week 2: One new component (WalkForwardValidator)
- Week 3: Docs & polish

**Risk**: Architecture astronauts trying to "improve" working code

**Mitigation**: Strict no-refactoring rule in Week 1

---

## Glossary

| Term | Definition |
|------|------------|
| **Walk-Forward Analysis** | Test strategy on unseen periods using parameters from previous optimization |
| **WF Efficiency** | Ratio of out-of-sample to in-sample performance (avgOOS / avgIS) |
| **Anchored Window** | Training window starts at beginning, expands over time |
| **Rolling Window** | Training window slides forward, maintains fixed size |
| **Overfitting** | Strategy works on historical data but fails on new data |
| **Sharpe Ratio** | Risk-adjusted return: (Return - RiskFree) / StdDev |
| **Sortino Ratio** | Like Sharpe but only penalizes downside volatility |

---

**Document Version**: 2.0 (Pragmatic Edition)
**Last Updated**: 2025-10-04
**Status**: Ready for 3-week implementation sprint
**Motto**: Ship working code, iterate based on feedback
