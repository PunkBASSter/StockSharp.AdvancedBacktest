# Priority Queue - StockSharp.AdvancedBacktest

## Phase 1: StockSharp Integration Foundation - HIGH PRIORITY

### Current Active Task Queue

**WIP Limit: 1 task maximum**

| Priority | Task ID | Epic | Task | Agent | Status | Dependencies |
|----------|---------|------|------|-------|--------|--------------|
| HIGH-01 | P1-CORE-01 | Phase1-Foundation | Create Enhanced Strategy Base Classes | dotnet-csharp-expert | READY | None |
| HIGH-02 | P1-CORE-02 | Phase1-Foundation | Implement ParameterSet with Validation | dotnet-csharp-expert | READY | P1-CORE-01 |
| HIGH-03 | P1-OPT-01 | Phase1-Foundation | Create BruteForceOptimizerWrapper | dotnet-csharp-expert | READY | P1-CORE-01, P1-CORE-02 |
| HIGH-04 | P1-PERF-01 | Phase1-Foundation | Implement PerformanceCalculator | quantum-trading-expert | READY | P1-OPT-01 |
| HIGH-05 | P1-DATA-01 | Phase1-Foundation | Create JsonSerializationService | dotnet-csharp-expert | READY | None |
| HIGH-06 | P1-DATA-02 | Phase1-Foundation | Implement ArtifactManager | data-architect | READY | P1-DATA-01 |
| HIGH-07 | P1-PIPE-01 | Phase1-Foundation | Create PipelineOrchestrator | solution-architect | READY | P1-OPT-01, P1-DATA-02 |
| HIGH-08 | P1-REPORT-01 | Phase1-Foundation | Implement ReportGenerator MVP | dotnet-csharp-expert | READY | P1-DATA-02 |

### Testing Tasks - MEDIUM PRIORITY

| Priority | Task ID | Epic | Task | Agent | Status | Dependencies |
|----------|---------|------|------|-------|--------|--------------|
| MED-01 | P1-TEST-01 | Phase1-Foundation | Unit Tests for Strategy Base Classes | dotnet-csharp-expert | READY | P1-CORE-01, P1-CORE-02 |
| MED-02 | P1-TEST-02 | Phase1-Foundation | Integration Tests for Optimizer Wrapper | dotnet-csharp-expert | READY | P1-OPT-01 |
| MED-03 | P1-TEST-03 | Phase1-Foundation | Performance Calculator Tests | quantum-trading-expert | READY | P1-PERF-01 |
| MED-04 | P1-TEST-04 | Phase1-Foundation | JSON Export and Artifact Tests | dotnet-csharp-expert | READY | P1-DATA-01, P1-DATA-02 |

### Dependencies Setup - LOW PRIORITY

| Priority | Task ID | Epic | Task | Agent | Status | Dependencies |
|----------|---------|------|------|-------|--------|--------------|
| LOW-01 | P1-SETUP-01 | Phase1-Foundation | Add StockSharp NuGet Dependencies | dotnet-csharp-expert | READY | None |
| LOW-02 | P1-SETUP-02 | Phase1-Foundation | Configure Modern .NET Dependencies | dotnet-csharp-expert | READY | None |
| LOW-03 | P1-SETUP-03 | Phase1-Foundation | Create Project Structure | solution-architect | READY | None |

## Task Assignment Rules

1. **Single Task Execution**: Only 1 task can be IN-PROGRESS at any time
2. **Sequential Dependencies**: Complete dependency tasks before starting dependent tasks
3. **Agent Specialization**: Assign tasks to appropriate specialist agents
4. **Testing Integration**: Include testing tasks after core functionality
5. **Milestone Tracking**: Mark epic completion when all HIGH priority tasks are DONE

## Epic Completion Criteria

**Phase 1 Foundation Epic Complete When:**
- All HIGH priority tasks are DONE
- All MEDIUM priority tests are DONE
- MVP functionality demonstrated working end-to-end
- JSON export produces valid artifacts
- Basic HTML reports are generated successfully

## Next Phase Preparation

- Phase 2 tasks will be added after Phase 1 completion
- Walk-forward analysis tasks ready for prioritization
- Monte Carlo validation tasks in backlog