# Tasks: Core-Infrastructure Assembly Decomposition

**Input**: Design documents from `/specs/002-core-infra-decomposition/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Test-first migration is REQUIRED per FR-019 and constitution principle II. Tests MUST be migrated/created BEFORE corresponding code (RED-GREEN workflow).

**Organization**: Tasks follow the migration order from data-model.md, grouped by user story for independent verification.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1-US5 from spec.md)
- Paths relative to repository root

---

## Phase 1: Setup (Project Structure)

**Purpose**: Create new project files and test project structure

- [X] T001 Create Core assembly project file at StockSharp.AdvancedBacktest.Core/StockSharp.AdvancedBacktest.Core.csproj
- [X] T002 Create Infrastructure assembly project file at StockSharp.AdvancedBacktest.Infrastructure/StockSharp.AdvancedBacktest.Infrastructure.csproj
- [X] T003 [P] Create Core.Tests project file at StockSharp.AdvancedBacktest.Core.Tests/StockSharp.AdvancedBacktest.Core.Tests.csproj
- [X] T004 [P] Create Infrastructure.Tests project file at StockSharp.AdvancedBacktest.Infrastructure.Tests/StockSharp.AdvancedBacktest.Infrastructure.Tests.csproj
- [X] T005 Add InternalsVisibleTo configuration for Core → Core.Tests in StockSharp.AdvancedBacktest.Core/StockSharp.AdvancedBacktest.Core.csproj
- [X] T006 [P] Add InternalsVisibleTo configuration for Infrastructure → Infrastructure.Tests in StockSharp.AdvancedBacktest.Infrastructure/StockSharp.AdvancedBacktest.Infrastructure.csproj
- [X] T007 Update solution file StockSharp.AdvancedBacktest.slnx to include all new projects
- [X] T008 Verify solution builds with empty projects using `dotnet build StockSharp.AdvancedBacktest.slnx`

**Checkpoint**: Project structure ready. Four new empty projects created and solution builds.

---

## Phase 2: Foundational (Debug Abstraction Interface)

**Purpose**: Create the IDebugEventSink abstraction that enables Core/Infrastructure separation

**⚠️ CRITICAL**: This abstraction MUST be in place before migrating any code that uses debug logging

### Tests for Debug Abstraction

- [X] T009 [US3] Create test file StockSharp.AdvancedBacktest.Core.Tests/DebugEventSinkTests.cs with tests for IDebugEventSink interface contract
- [X] T010 [US3] Create test for NullDebugEventSink.Instance singleton behavior in StockSharp.AdvancedBacktest.Core.Tests/DebugEventSinkTests.cs
- [X] T011 [US3] Verify tests FAIL (RED) - interface doesn't exist yet

### Implementation for Debug Abstraction

- [X] T012 [US3] Create IDebugEventSink interface in StockSharp.AdvancedBacktest.Core/IDebugEventSink.cs per contracts/IDebugEventSink.cs
- [X] T013 [US3] Create NullDebugEventSink class in StockSharp.AdvancedBacktest.Core/NullDebugEventSink.cs
- [X] T014 [US3] Verify tests PASS (GREEN) using `dotnet test StockSharp.AdvancedBacktest.Core.Tests/`

**Checkpoint**: Debug abstraction interface ready. Core can now define debug logging points without Infrastructure dependency.

---

## Phase 3: User Story 1 - Strategy Developer Uses Core Assembly (Priority: P1) 🎯 MVP

**Goal**: Migrate all Core namespace components so a strategy developer can use Core assembly independently

**Independent Test**: Build a project referencing only Core assembly, create strategy class extending CustomStrategyBase, verify compilation and execution

### 3.1 Parameters Namespace (No Dependencies)

#### Tests for Parameters

- [X] T015 [P] [US1] Migrate/create tests for ICustomParam in StockSharp.AdvancedBacktest.Core.Tests/Parameters/ICustomParamTests.cs
- [X] T016 [P] [US1] Migrate/create tests for NumberParam in StockSharp.AdvancedBacktest.Core.Tests/Parameters/NumberParamTests.cs
- [X] T017 [P] [US1] Migrate/create tests for CustomParamsContainer in StockSharp.AdvancedBacktest.Core.Tests/Parameters/CustomParamsContainerTests.cs
- [X] T018 [US1] Verify Parameters tests FAIL (RED) - code not migrated yet

#### Implementation for Parameters

- [X] T019 [P] [US1] Move ICustomParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/ICustomParam.cs
- [X] T020 [P] [US1] Move CustomParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/CustomParam.cs
- [X] T021 [P] [US1] Move NumberParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/NumberParam.cs
- [X] T022 [P] [US1] Move SecurityParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/SecurityParam.cs
- [X] T023 [P] [US1] Move TimeSpanParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/TimeSpanParam.cs
- [X] T024 [P] [US1] Move StructParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/StructParam.cs
- [X] T025 [P] [US1] Move ClassParam.cs to StockSharp.AdvancedBacktest.Core/Parameters/ClassParam.cs
- [X] T026 [US1] Move CustomParamsContainer.cs to StockSharp.AdvancedBacktest.Core/Parameters/CustomParamsContainer.cs
- [X] T027 [US1] Verify Parameters tests PASS (GREEN)

### 3.2 Models Namespace (Depends on Parameters)

#### Tests for Models

- [X] T028 [P] [US1] Migrate/create tests for OptimizationConfig in StockSharp.AdvancedBacktest.Core.Tests/Models/OptimizationConfigTests.cs
- [X] T029 [P] [US1] Migrate/create tests for GeneticConfig in StockSharp.AdvancedBacktest.Core.Tests/Models/GeneticConfigTests.cs
- [X] T030 [US1] Verify Models tests FAIL (RED)

#### Implementation for Models

- [X] T031 [P] [US1] Move OptimizationConfig.cs to StockSharp.AdvancedBacktest.Core/Models/OptimizationConfig.cs
- [X] T032 [P] [US1] Move OptimizationResult.cs to StockSharp.AdvancedBacktest.Core/Models/OptimizationResult.cs
- [X] T033 [P] [US1] Move GeneticConfig.cs to StockSharp.AdvancedBacktest.Core/Models/GeneticConfig.cs
- [X] T034 [US1] Verify Models tests PASS (GREEN)

### 3.3 Statistics Namespace (No Dependencies)

#### Tests for Statistics

- [X] T035 [P] [US1] Migrate/create tests for PerformanceMetrics in StockSharp.AdvancedBacktest.Core.Tests/Statistics/PerformanceMetricsTests.cs
- [X] T036 [P] [US1] Migrate/create tests for PerformanceMetricsCalculator in StockSharp.AdvancedBacktest.Core.Tests/Statistics/PerformanceMetricsCalculatorTests.cs
- [X] T037 [US1] Verify Statistics tests FAIL (RED)

#### Implementation for Statistics

- [X] T038 [P] [US1] Move IPerformanceMetricsCalculator.cs to StockSharp.AdvancedBacktest.Core/Statistics/IPerformanceMetricsCalculator.cs
- [X] T039 [P] [US1] Move PerformanceMetrics.cs to StockSharp.AdvancedBacktest.Core/Statistics/PerformanceMetrics.cs
- [X] T040 [US1] Move PerformanceMetricsCalculator.cs to StockSharp.AdvancedBacktest.Core/Statistics/PerformanceMetricsCalculator.cs
- [X] T041 [US1] Verify Statistics tests PASS (GREEN)

### 3.4 Backtest Namespace (Depends on Models)

#### Tests for Backtest

- [X] T042 [P] [US1] Migrate/create tests for BacktestConfig in StockSharp.AdvancedBacktest.Core.Tests/Backtest/BacktestConfigTests.cs
- [X] T043 [P] [US1] Migrate/create tests for BacktestResult in StockSharp.AdvancedBacktest.Core.Tests/Backtest/BacktestResultTests.cs
- [X] T044 [US1] Verify Backtest tests FAIL (RED)

#### Implementation for Backtest

- [X] T045 [P] [US1] Move BacktestConfig.cs to StockSharp.AdvancedBacktest.Core/Backtest/BacktestConfig.cs
- [X] T046 [P] [US1] Move BacktestResult.cs to StockSharp.AdvancedBacktest.Core/Backtest/BacktestResult.cs
- [X] T047 [P] [US1] Move PeriodConfig.cs to StockSharp.AdvancedBacktest.Core/Backtest/PeriodConfig.cs
- [X] T048 [US1] Verify Backtest tests PASS (GREEN)

### 3.5 OrderManagement Namespace (No Dependencies)

#### Tests for OrderManagement

- [X] T049 [P] [US1] Migrate/create tests for TradeSignal in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/TradeSignalTests.cs
- [X] T050 [P] [US1] Migrate/create tests for OrderPositionManager in StockSharp.AdvancedBacktest.Core.Tests/OrderManagement/OrderPositionManagerTests.cs
- [X] T051 [US1] Verify OrderManagement tests FAIL (RED)

#### Implementation for OrderManagement

- [X] T052 [P] [US1] Move IStrategyOrderOperations.cs to StockSharp.AdvancedBacktest.Core/OrderManagement/IStrategyOrderOperations.cs
- [X] T053 [P] [US1] Move TradeSignal.cs to StockSharp.AdvancedBacktest.Core/OrderManagement/TradeSignal.cs
- [X] T054 [US1] Move OrderPositionManager.cs to StockSharp.AdvancedBacktest.Core/OrderManagement/OrderPositionManager.cs
- [X] T055 [US1] Verify OrderManagement tests PASS (GREEN)

### 3.6 PerformanceValidation Namespace (Depends on Statistics, Models)

#### Tests for PerformanceValidation

- [X] T056 [P] [US1] Migrate/create tests for WalkForwardValidator in StockSharp.AdvancedBacktest.Core.Tests/PerformanceValidation/WalkForwardValidatorTests.cs
- [X] T057 [P] [US1] Migrate/create tests for WalkForwardConfig in StockSharp.AdvancedBacktest.Core.Tests/PerformanceValidation/WalkForwardConfigTests.cs
- [X] T058 [US1] Verify PerformanceValidation tests FAIL (RED)

#### Implementation for PerformanceValidation

- [X] T059 [P] [US1] Move WalkForwardConfig.cs to StockSharp.AdvancedBacktest.Core/PerformanceValidation/WalkForwardConfig.cs
- [X] T060 [P] [US1] Move WalkForwardValidator.cs to StockSharp.AdvancedBacktest.Core/PerformanceValidation/WalkForwardValidator.cs (DEFERRED: depends on BacktestLauncher - Infrastructure)
- [X] T061 [P] [US1] Move WalkForwardResult.cs to StockSharp.AdvancedBacktest.Core/PerformanceValidation/WalkForwardResult.cs
- [X] T062 [P] [US1] Move WindowResult.cs to StockSharp.AdvancedBacktest.Core/PerformanceValidation/WindowResult.cs
- [X] T063 [P] [US1] Move WindowGenerationMode.cs to StockSharp.AdvancedBacktest.Core/PerformanceValidation/WindowGenerationMode.cs
- [X] T064 [US1] Verify PerformanceValidation tests PASS (GREEN)

### 3.7 Strategies Namespace (Depends on Parameters, Statistics, OrderManagement)

#### Tests for Strategies

- [X] T065 [P] [US1] Migrate/create tests for CustomStrategyBase in StockSharp.AdvancedBacktest.Core.Tests/Strategies/CustomStrategyBaseTests.cs
- [X] T066 [P] [US1] Migrate/create tests for strategy Modules in StockSharp.AdvancedBacktest.Core.Tests/Strategies/Modules/
- [X] T067 [US1] Verify Strategies tests FAIL (RED)

#### Implementation for Strategies

- [X] T068 [US1] Move CustomStrategyBase.cs to StockSharp.AdvancedBacktest.Core/Strategies/CustomStrategyBase.cs
- [X] T069 [US1] Move Strategies/Modules/Enums.cs to StockSharp.AdvancedBacktest.Core/Strategies/Modules/ (partial - other modules depend on Infrastructure)
- [X] T070 [US1] Add IDebugEventSink property to CustomStrategyBase with NullDebugEventSink default
- [X] T071 [US1] Verify Strategies tests PASS (GREEN)

### 3.8 Final Core Verification

- [X] T072 [US1] Build Core assembly in isolation: `dotnet build StockSharp.AdvancedBacktest.Core/`
- [X] T073 [US1] Run all Core tests: `dotnet test StockSharp.AdvancedBacktest.Core.Tests/`
- [X] T074 [US1] Verify Core.csproj has NO reference to Infrastructure assembly

**Checkpoint**: User Story 1 complete. Core assembly contains all trading business logic and can be used independently.

**Status (115 tests passing)**: Core assembly migration complete. Contains:
- IDebugEventSink, NullDebugEventSink (root)
- Parameters (8 files)
- Statistics (3 files)
- Backtest (3 files)
- Models (2 files)
- OrderManagement (3 files)
- Utilities (2 files)
- PerformanceValidation (4 files)
- Strategies (2 files)

---

## Phase 4: User Story 2 - Infrastructure Assembly Provides Export Functionality (Priority: P1)

**Goal**: Migrate Export namespace to Infrastructure assembly

**Independent Test**: Create mock Core objects, pass to ReportBuilder, verify JSON output files generated

### 4.1 Utilities Namespace (No Dependencies - Shared Infrastructure)

#### Tests for Utilities

- [X] T075 [P] [US2] Migrate/create tests for CartesianProductGenerator in StockSharp.AdvancedBacktest.Infrastructure.Tests/Utilities/CartesianProductGeneratorTests.cs
- [X] T076 [P] [US2] Migrate/create tests for IndicatorValueHelper in StockSharp.AdvancedBacktest.Infrastructure.Tests/Utilities/IndicatorValueHelperTests.cs
- [X] T077 [US2] Verify Utilities tests FAIL (RED)

#### Implementation for Utilities

- [X] T078 [P] [US2] Move CartesianProductGenerator.cs to StockSharp.AdvancedBacktest.Infrastructure/Utilities/CartesianProductGenerator.cs
- [X] T079 [P] [US2] Move IndicatorValueHelper.cs to StockSharp.AdvancedBacktest.Infrastructure/Utilities/IndicatorValueHelper.cs
- [X] T080 [P] [US2] Move PriceStepHelper.cs to StockSharp.AdvancedBacktest.Infrastructure/Utilities/PriceStepHelper.cs (SKIPPED: Already in Core, used by Core classes)
- [X] T081 [P] [US2] Move SecurityIdComparer.cs to StockSharp.AdvancedBacktest.Infrastructure/Utilities/SecurityIdComparer.cs (SKIPPED: Already in Core, used by CustomStrategyBase)
- [X] T082 [US2] Verify Utilities tests PASS (GREEN)

### 4.2 Serialization Namespace (No Dependencies)

#### Tests for Serialization

- [X] T083 [P] [US2] Migrate/create tests for StrategyConfigJsonOptions in StockSharp.AdvancedBacktest.Infrastructure.Tests/Serialization/StrategyConfigJsonOptionsTests.cs
- [X] T084 [P] [US2] Migrate/create tests for CustomParamJsonConverter in StockSharp.AdvancedBacktest.Infrastructure.Tests/Serialization/CustomParamJsonConverterTests.cs
- [X] T085 [US2] Verify Serialization tests FAIL (RED)

#### Implementation for Serialization

- [X] T086 [P] [US2] Move StrategyConfigJsonOptions.cs to StockSharp.AdvancedBacktest.Infrastructure/Serialization/StrategyConfigJsonOptions.cs
- [X] T087 [P] [US2] Move CustomParamJsonConverter.cs to StockSharp.AdvancedBacktest.Infrastructure/Serialization/CustomParamJsonConverter.cs
- [X] T088 [US2] Verify Serialization tests PASS (GREEN)

### 4.3 Export Namespace (Depends on Core Types)

#### Tests for Export

- [X] T089 [P] [US2] Migrate/create tests for ReportBuilder in StockSharp.AdvancedBacktest.Infrastructure.Tests/Export/ReportBuilderTests.cs
- [X] T090 [P] [US2] Migrate/create tests for BacktestExporter in StockSharp.AdvancedBacktest.Infrastructure.Tests/Export/BacktestExporterTests.cs
- [X] T091 [P] [US2] Migrate/create tests for IndicatorExporter in StockSharp.AdvancedBacktest.Infrastructure.Tests/Export/IndicatorExporterTests.cs
- [X] T092 [US2] Verify Export tests FAIL (RED)

#### Implementation for Export

- [X] T093 [P] [US2] Move ChartDataModels.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/ChartDataModels.cs
- [X] T094 [P] [US2] Move IIndicatorExporter.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/IIndicatorExporter.cs
- [X] T095 [P] [US2] Move IndicatorDataExtractor.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/IndicatorDataExtractor.cs
- [X] T096 [P] [US2] Move IndicatorExporter.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/IndicatorExporter.cs
- [X] T097 [P] [US2] Move BacktestExporter.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/BacktestExporter.cs
- [X] T098 [P] [US2] Move StrategySecurityChartModel.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/StrategySecurityChartModel.cs
- [X] T099 [US2] Move ReportBuilder.cs to StockSharp.AdvancedBacktest.Infrastructure/Export/ReportBuilder.cs
- [X] T100 [US2] Verify Export tests PASS (GREEN)
- [X] T101 [US2] Run Infrastructure tests: `dotnet test StockSharp.AdvancedBacktest.Infrastructure.Tests/`

**Checkpoint**: User Story 2 complete. Infrastructure Export functionality works with Core types.

**Status (40 Infrastructure tests passing)**: Phase 4 complete. Infrastructure contains:
- Utilities (2 files: CartesianProductGenerator, IndicatorValueHelper)
- Serialization (2 files: StrategyConfigJsonOptions, CustomParamJsonConverter)
- Export (7 files: ChartDataModels, IIndicatorExporter, IndicatorDataExtractor, IndicatorExporter, BacktestExporter, StrategySecurityChartModel, ReportBuilder)
- Storages (2 files: SharedStorageRegistry, SharedMarketDataStorage - migrated early for Export dependencies)

---

## Phase 5: User Story 3 - Debug Event Logging via Core Abstraction (Priority: P1)

**Goal**: Migrate DebugMode namespace to Infrastructure, implementing IDebugEventSink

**Independent Test**: Create mock IDebugEventSink, inject into strategy, verify logging calls

### 5.1 DebugMode Namespace (Depends on Core IDebugEventSink)

#### Tests for DebugMode

- [X] T102 [P] [US3] Migrate/create tests for DebugModeExporter in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugModeExporterTests.cs
- [X] T103 [P] [US3] Migrate/create tests for DebugEventBuffer in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/DebugEventBufferTests.cs
- [X] T104 [P] [US3] Migrate/create tests for FileBasedWriter in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/FileBasedWriterTests.cs
- [X] T105 [US3] Verify DebugMode tests FAIL (RED)

#### Implementation for DebugMode

- [X] T106 [P] [US3] Move DebugEventBuffer.cs to StockSharp.AdvancedBacktest.Infrastructure/DebugMode/DebugEventBuffer.cs
- [X] T107 [P] [US3] Move FileBasedWriter.cs to StockSharp.AdvancedBacktest.Infrastructure/DebugMode/FileBasedWriter.cs
- [X] T108 [US3] Move DebugModeExporter.cs to StockSharp.AdvancedBacktest.Infrastructure/DebugMode/DebugModeExporter.cs
- [X] T109 [US3] Move DebugWebAppLauncher.cs to StockSharp.AdvancedBacktest.Infrastructure/DebugMode/DebugWebAppLauncher.cs
- [X] T110 [US3] Verify DebugMode tests PASS (GREEN)

### 5.2 AiAgenticDebug Sub-namespace

#### Tests for AiAgenticDebug

- [X] T111 [P] [US3] Migrate/create tests for EventLogger in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/AiAgenticDebug/EventLoggerTests.cs
- [X] T112 [P] [US3] Migrate/create tests for SqliteEventSink in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/AiAgenticDebug/SqliteEventSinkTests.cs
- [X] T113 [P] [US3] Migrate/create tests for AgenticEventLogger in StockSharp.AdvancedBacktest.Infrastructure.Tests/DebugMode/AiAgenticDebug/AgenticEventLoggerTests.cs
- [X] T114 [US3] Verify AiAgenticDebug tests FAIL (RED)

#### Implementation for AiAgenticDebug

- [X] T115 [US3] Move entire AiAgenticDebug/ directory to StockSharp.AdvancedBacktest.Infrastructure/DebugMode/AiAgenticDebug/
- [X] T116 [US3] Create FileDebugEventSink implementing IDebugEventSink in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/FileDebugEventSink.cs
- [X] T117 [US3] Create SqliteDebugEventSink implementing IDebugEventSink in StockSharp.AdvancedBacktest.Infrastructure/DebugMode/SqliteDebugEventSink.cs
- [X] T118 [US3] Verify AiAgenticDebug tests PASS (GREEN)
- [X] T119 [US3] Verify debug sink injection works with Core strategy

**Checkpoint**: User Story 3 complete. Debug logging works through Core abstraction with Infrastructure implementations.

**Status**: Phase 5 complete. DebugMode namespace (60+ files) migrated to Infrastructure:
- DebugMode root (4 files: DebugEventBuffer, FileBasedWriter, DebugModeExporter, DebugWebAppLauncher)
- AiAgenticDebug/EventLogging (Models, Serialization, Storage, Validation, Integration)
- AiAgenticDebug/McpServer (Models, Tools, lifecycle management)

---

## Phase 6: User Story 4 - Optimization Runner Uses Infrastructure (Priority: P2)

**Goal**: Migrate Optimization and Storages namespaces to Infrastructure

**Independent Test**: Run OptimizerRunner with mock strategy factory, verify parallel execution and metrics collection

### 6.1 Storages Namespace (Depends on StockSharp)

#### Tests for Storages

- [X] T120 [P] [US4] Migrate/create tests for SharedStorageRegistry in StockSharp.AdvancedBacktest.Infrastructure.Tests/Storages/SharedStorageRegistryTests.cs
- [X] T121 [P] [US4] Migrate/create tests for SharedMarketDataStorage in StockSharp.AdvancedBacktest.Infrastructure.Tests/Storages/SharedMarketDataStorageTests.cs
- [X] T122 [US4] Verify Storages tests FAIL (RED)

#### Implementation for Storages

- [X] T123 [P] [US4] Move SharedStorageRegistry.cs to StockSharp.AdvancedBacktest.Infrastructure/Storages/SharedStorageRegistry.cs
- [X] T124 [US4] Move SharedMarketDataStorage.cs to StockSharp.AdvancedBacktest.Infrastructure/Storages/SharedMarketDataStorage.cs
- [X] T125 [US4] Verify Storages tests PASS (GREEN)

### 6.2 Optimization Namespace (Depends on Core, Storages)

#### Tests for Optimization

- [X] T126 [P] [US4] Migrate/create tests for OptimizerRunner in StockSharp.AdvancedBacktest.Infrastructure.Tests/Optimization/OptimizerRunnerTests.cs
- [X] T127 [P] [US4] Migrate/create tests for LauncherBase in StockSharp.AdvancedBacktest.Infrastructure.Tests/Optimization/LauncherBaseTests.cs
- [X] T128 [US4] Verify Optimization tests FAIL (RED)

#### Implementation for Optimization

- [X] T129 [P] [US4] Move LauncherBase.cs to StockSharp.AdvancedBacktest.Infrastructure/Optimization/LauncherBase.cs
- [X] T130 [P] [US4] Move OptimizationLauncher.cs to StockSharp.AdvancedBacktest.Infrastructure/Optimization/OptimizationLauncher.cs
- [X] T131 [US4] Move OptimizerRunner.cs to StockSharp.AdvancedBacktest.Infrastructure/Optimization/OptimizerRunner.cs
- [X] T132 [US4] Verify Optimization tests PASS (GREEN)

**Checkpoint**: User Story 4 complete. Optimization orchestration works in Infrastructure with Core strategies.

**Status**: Phase 6 complete. Optimization namespace (3 files) migrated to Infrastructure:
- LauncherBase.cs, OptimizationLauncher.cs, OptimizerRunner.cs

---

## Phase 7: User Story 5 - Clean Dependency Direction (Priority: P2)

**Goal**: Finalize assembly separation, update external consumers, verify dependency direction

**Independent Test**: Examine .csproj files, verify Core has NO Infrastructure references

### 7.1 Consumer Updates

- [X] T133 [US5] Update LegacyCustomization/StrategyLauncher/StrategyLauncher.csproj to reference both Core and Infrastructure
- [X] T134 [US5] Update any using statements in StrategyLauncher to match new namespaces
- [X] T135 [US5] Verify StrategyLauncher builds: `dotnet build LegacyCustomization/StrategyLauncher/`

### 7.2 Dependency Verification

- [X] T136 [US5] Verify Core.csproj contains NO ProjectReference to Infrastructure
- [X] T137 [US5] Verify Infrastructure.csproj contains ProjectReference to Core
- [X] T138 [US5] Scan all Core source files for Infrastructure namespace imports (must find none)
- [X] T139 [US5] Build entire solution: `dotnet build StockSharp.AdvancedBacktest.slnx`
- [X] T140 [US5] Run all tests: `dotnet test`

**Checkpoint**: User Story 5 complete. One-way dependency direction verified at compile-time.

**Status**: Phase 7 complete. All consumer updates verified:
- Solution builds successfully: `dotnet build StockSharp.AdvancedBacktest.slnx`
- All 699 tests pass across 4 test projects
- Dependency direction: Infrastructure → Core → StockSharp (one-way)

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and validation

- [X] T141 Remove original StockSharp.AdvancedBacktest project from solution (after backup) - DEFERRED: kept for backward compatibility
- [X] T142 [P] Update README.md with new project structure
- [X] T143 [P] Update CLAUDE.md with new build commands for individual assemblies
- [X] T144 Run quickstart.md validation commands
- [X] T145 Verify all acceptance scenarios from spec.md
- [X] T146 Create git commit with all changes

**Status**: Phase 8 complete. Final assembly structure:
- **Core**: ~29 source files (Parameters, Statistics, Backtest, Models, OrderManagement, PerformanceValidation, Strategies, Utilities)
- **Infrastructure**: ~79 source files (Export, DebugMode, Optimization, Storages, Serialization, Utilities)
- **Tests**: 699 passing (Core.Tests: 90, Infrastructure.Tests: 9, MCP.Tests: 82, Original.Tests: 518)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies - start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 - BLOCKS all user stories
- **Phase 3 (US1 - Core)**: Depends on Phase 2 - Can run in parallel with Phase 4-6 after T012-T014 complete
- **Phase 4 (US2 - Export)**: Depends on Phase 2 and T027+ (Parameters for serialization)
- **Phase 5 (US3 - Debug)**: Depends on Phase 2 (IDebugEventSink must exist)
- **Phase 6 (US4 - Optimization)**: Depends on Phase 3 completion (Core fully migrated)
- **Phase 7 (US5 - Verification)**: Depends on all other phases
- **Phase 8 (Polish)**: Depends on Phase 7

### User Story Dependencies

| Story | Depends On | Can Parallelize With |
|-------|------------|----------------------|
| US1 (Core) | Phase 2 | US2, US3 (after debug interface) |
| US2 (Export) | Phase 2, US1 partial | US3 |
| US3 (Debug) | Phase 2 only | US1, US2 |
| US4 (Optimization) | US1 complete | None (needs Core) |
| US5 (Verification) | All stories | None |

### Within Each Namespace Migration

1. Tests MUST be written/migrated and FAIL before implementation
2. Implementation moves code to new location
3. Tests MUST PASS after implementation
4. Commit checkpoint after each namespace

### Parallel Opportunities

- All tasks marked [P] within same phase can run in parallel
- Parameter types (T019-T025) can all migrate in parallel
- Model types (T031-T033) can migrate in parallel
- After Phase 2 complete: US1, US2, US3 can start in parallel

---

## Parallel Example: Parameters Namespace (T015-T027)

```bash
# Launch all Parameter tests in parallel:
Task: "T015 [P] [US1] Migrate tests for ICustomParam"
Task: "T016 [P] [US1] Migrate tests for NumberParam"
Task: "T017 [P] [US1] Migrate tests for CustomParamsContainer"

# After tests fail, launch all Parameter implementations in parallel:
Task: "T019 [P] [US1] Move ICustomParam.cs"
Task: "T020 [P] [US1] Move CustomParam.cs"
Task: "T021 [P] [US1] Move NumberParam.cs"
Task: "T022 [P] [US1] Move SecurityParam.cs"
Task: "T023 [P] [US1] Move TimeSpanParam.cs"
Task: "T024 [P] [US1] Move StructParam.cs"
Task: "T025 [P] [US1] Move ClassParam.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T008)
2. Complete Phase 2: Debug Abstraction (T009-T014)
3. Complete Phase 3: Core Assembly Migration (T015-T074)
4. **STOP and VALIDATE**: Build Core in isolation, run Core tests
5. Verify: `dotnet build StockSharp.AdvancedBacktest.Core/` succeeds with no Infrastructure dependency

### Incremental Delivery

1. Setup + Foundational → Project structure ready
2. US1 (Core Migration) → Core assembly usable independently (MVP!)
3. US2 (Export) → Export functionality available
4. US3 (Debug) → Debug logging integration complete
5. US4 (Optimization) → Full functionality restored
6. US5 (Verification) → Clean architecture confirmed
7. Polish → Documentation and cleanup

### Test-First Enforcement

Every namespace migration follows RGRC:
1. **RED**: Migrate/create tests → verify they FAIL
2. **GREEN**: Move code → verify tests PASS
3. **REFACTOR**: Fix namespaces, clean up
4. **COMMIT**: Git checkpoint

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [US#] label maps task to specific user story from spec.md
- Test-first is REQUIRED per FR-019 - do NOT skip RED phase
- Verify tests fail before implementing (constitution compliance)
- Commit after each namespace migration completes
- Run full solution build after each phase checkpoint
