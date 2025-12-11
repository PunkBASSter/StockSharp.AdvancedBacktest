# Research: Core-Infrastructure Assembly Decomposition

**Date**: 2025-12-09
**Feature**: 002-core-infra-decomposition

## 1. Debug Logging Abstraction Design

### Decision
Use a minimal custom `IDebugEventSink` interface in Core with Null Object pattern for no-op behavior.

### Rationale
- Keeps Core free from external dependencies (Microsoft.Extensions.Logging not needed in Core)
- Null Object pattern eliminates null checks throughout strategy code
- Simple interface aligns with constitution principle of composition over inheritance
- Infrastructure can implement with any backing store (SQLite, file, console)

### Implementation Pattern

```csharp
// Core: IDebugEventSink.cs
namespace StockSharp.AdvancedBacktest.Core
{
    public interface IDebugEventSink
    {
        void LogEvent(string category, string eventType, object data);
        void Flush();
    }

    // Null object default - no-op when not configured
    public sealed class NullDebugEventSink : IDebugEventSink
    {
        public static readonly NullDebugEventSink Instance = new();
        private NullDebugEventSink() { }
        public void LogEvent(string category, string eventType, object data) { }
        public void Flush() { }
    }
}
```

### Alternatives Considered
- **Microsoft.Extensions.Logging directly in Core**: Rejected - adds unnecessary dependency to Core assembly
- **Event-based system**: Rejected - more complex, not needed for debug logging
- **Manual null checks**: Rejected - verbose, error-prone

---

## 2. Test-First Migration Strategy

### Decision
Use RGRC (Red-Green-Refactor-Commit) pattern with stub assemblies.

### Rationale
- Aligns with constitution principle II (Test-First Development)
- Git checkpoints provide rollback safety during refactoring
- Stub assemblies allow test compilation before code migration
- Each component migration is independently verifiable

### Migration Workflow

1. **Create test project structure** (empty, referencing future assemblies)
2. **Per-component cycle**:
   - RED: Write/migrate tests to new location
   - GREEN: Create stub or move code to make tests compile
   - REFACTOR: Complete code migration, fix namespaces
   - COMMIT: Checkpoint after each component

### Alternatives Considered
- **Code-first migration**: Rejected - higher risk, no test validation during move
- **Big-bang migration**: Rejected - too risky, hard to isolate issues
- **Parallel duplication**: Rejected - violates DRY, maintenance burden

---

## 3. One-Way Dependency Enforcement

### Decision
Use project references with access modifiers; no type forwarding needed.

### Rationale
- Compile-time enforcement via project reference structure
- FR-018 preserves namespaces, so no backward compatibility shims needed
- `internal` visibility hides implementation details within each assembly
- Simple verification: examine `.csproj` references

### Project Reference Structure

```xml
<!-- Core.csproj - NO references to Infrastructure -->
<ItemGroup>
  <ProjectReference Include="..\StockSharp\...\*.csproj" />
</ItemGroup>

<!-- Infrastructure.csproj - References Core -->
<ItemGroup>
  <ProjectReference Include="..\Core\StockSharp.AdvancedBacktest.Core.csproj" />
</ItemGroup>
```

### Alternatives Considered
- **Type forwarding**: Rejected - not needed since namespaces preserved
- **Shared project**: Rejected - violates separation of concerns
- **Runtime enforcement**: Rejected - compile-time is stronger

---

## 4. InternalsVisibleTo Configuration

### Decision
Use modern .csproj syntax for InternalsVisibleTo declarations.

### Rationale
- Centralized configuration in project file
- Self-documenting and auditable
- Aligns with SDK-style project format used in repository

### Implementation

```xml
<!-- StockSharp.AdvancedBacktest.Core.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="StockSharp.AdvancedBacktest.Core.Tests" />
</ItemGroup>

<!-- StockSharp.AdvancedBacktest.Infrastructure.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="StockSharp.AdvancedBacktest.Infrastructure.Tests" />
</ItemGroup>
```

### Alternatives Considered
- **AssemblyInfo.cs attributes**: Rejected - legacy approach, less centralized
- **Make everything public**: Rejected - sacrifices encapsulation

---

## 5. Project File Migration

### Decision
IDE-assisted refactoring with manual verification and SDK-style auto-globbing.

### Rationale
- Visual Studio/Rider handle namespace updates automatically
- SDK-style projects auto-include `*.cs` files, no manual editing
- Git diff provides clear audit trail of changes
- Build and test verification after each migration step

### Migration Steps per Component

1. Create target project structure
2. Use IDE "Move to" refactoring or manual file move
3. Verify namespace declarations match new location
4. Run `dotnet build` to verify compilation
5. Run `dotnet test` to verify behavior
6. Git commit checkpoint

### Alternatives Considered
- **Manual file copying**: Rejected - error-prone namespace mismatches
- **Code generation**: Rejected - unnecessary complexity for refactoring

---

## Summary

| Area | Decision | Justification |
|------|----------|---------------|
| Debug Abstraction | Custom IDebugEventSink + NullObject | Minimal Core dependency, testable |
| Test Migration | RGRC pattern | Constitution compliance, safe rollback |
| Dependency Direction | Project references + access modifiers | Compile-time enforcement |
| InternalsVisibleTo | Modern .csproj syntax | Centralized, auditable |
| Project Migration | IDE-assisted with verification | Automatic namespace handling |
