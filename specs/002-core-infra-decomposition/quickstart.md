# Quickstart: Core-Infrastructure Assembly Decomposition

**Date**: 2025-12-09
**Feature**: 002-core-infra-decomposition

## Prerequisites

- .NET 10 SDK installed
- StockSharp submodule initialized: `git submodule update --init --recursive`
- Current solution builds: `dotnet build StockSharp.AdvancedBacktest.slnx`
- All tests pass: `dotnet test`

## Project Structure After Decomposition

```
StockSharp.AdvancedBacktest/
├── StockSharp.AdvancedBacktest.Core/           # Business logic
├── StockSharp.AdvancedBacktest.Infrastructure/ # Implementations
├── StockSharp.AdvancedBacktest.Core.Tests/     # Core tests
├── StockSharp.AdvancedBacktest.Infrastructure.Tests/ # Infrastructure tests
└── LegacyCustomization/StrategyLauncher/       # Updated references
```

## Build Commands

```bash
# Build entire solution
dotnet build StockSharp.AdvancedBacktest.slnx

# Build Core only (verify isolation)
dotnet build StockSharp.AdvancedBacktest.Core/StockSharp.AdvancedBacktest.Core.csproj

# Build Infrastructure (requires Core)
dotnet build StockSharp.AdvancedBacktest.Infrastructure/StockSharp.AdvancedBacktest.Infrastructure.csproj
```

## Test Commands

```bash
# Run all tests
dotnet test

# Run Core tests only (verify Core isolation)
dotnet test StockSharp.AdvancedBacktest.Core.Tests/

# Run Infrastructure tests only
dotnet test StockSharp.AdvancedBacktest.Infrastructure.Tests/
```

## Migration Workflow (Per Component)

### 1. RED: Create/Migrate Tests

```bash
# Example: Migrating Parameters namespace tests
# 1. Create test file in new project
# 2. Update namespace to match new assembly
# 3. Verify tests fail (assembly doesn't exist yet)
dotnet build StockSharp.AdvancedBacktest.Core.Tests/  # Should fail
```

### 2. GREEN: Create Stub or Move Code

```bash
# 1. Move source files to new assembly
# 2. Update namespace declarations
# 3. Verify tests now compile and pass
dotnet test StockSharp.AdvancedBacktest.Core.Tests/  # Should pass
```

### 3. COMMIT: Checkpoint

```bash
git add .
git commit -m "Migrate Parameters namespace to Core assembly"
```

## Using Debug Logging Abstraction

### In Core (Strategy Code)

```csharp
using StockSharp.AdvancedBacktest.Core;

public class MyStrategy : CustomStrategyBase
{
    // Default: NullDebugEventSink (no-op)
    public IDebugEventSink DebugSink { get; set; } = NullDebugEventSink.Instance;

    protected override void OnCandleReceived(ICandleMessage candle)
    {
        // Logging happens through abstraction
        DebugSink.LogEvent("Candle", "Received", new {
            Time = candle.OpenTime,
            Close = candle.ClosePrice
        });
    }
}
```

### In Infrastructure (Configuration)

```csharp
using StockSharp.AdvancedBacktest.Infrastructure.DebugMode;

// Configure strategy with file-based debug logging
var strategy = new MyStrategy();
strategy.DebugSink = new FileDebugEventSink(outputPath);

// Or SQLite-based
strategy.DebugSink = new SqliteDebugEventSink(connectionString);
```

## Dependency Direction Verification

```bash
# Core should have NO reference to Infrastructure
Select-String -Path "StockSharp.AdvancedBacktest.Core/*.csproj" -Pattern "Infrastructure"
# Expected: No matches

# Infrastructure should reference Core
Select-String -Path "StockSharp.AdvancedBacktest.Infrastructure/*.csproj" -Pattern "Core"
# Expected: ProjectReference to Core.csproj
```

## Common Issues

### Issue: Circular dependency detected

**Cause**: Core class references Infrastructure type
**Fix**: Extract interface to Core, implement in Infrastructure

### Issue: Tests fail after move

**Cause**: Namespace mismatch or missing using
**Fix**: Verify namespace declarations match new assembly location

### Issue: LegacyCustomization doesn't build

**Cause**: Missing reference to new assemblies
**Fix**: Add ProjectReference to both Core and Infrastructure in StrategyLauncher.csproj
