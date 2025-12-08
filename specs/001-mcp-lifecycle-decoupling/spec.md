# Feature Specification: MCP Server Lifecycle Decoupling

**Feature Branch**: `001-mcp-lifecycle-decoupling`
**Created**: 2025-12-08
**Status**: Draft
**Input**: User description: "Decouple MCP server lifecycle from strategy execution: keep MCP accessible after backtest completion, ensure single MCP instance working with latest SQLite DB, implement SQLite cleanup on backtest start"

## Clarifications

### Session 2025-12-08

- Q: When/how should the MCP server initially start? → A: Auto-start on first --ai-debug backtest - MCP server launches automatically when first debug backtest runs, then stays alive
- Q: How should single MCP instance be detected/enforced? → A: Named mutex/semaphore - Use OS-level named synchronization primitive
- Q: How should MCP server be notified of database changes? → A: File system watcher - MCP server monitors the database file path for changes/recreation
- Q: How should MCP server survive backtest process exit? → A: Separate exe project (`StockSharp.AdvancedBacktest.DebugEventLogMcpServer`) - MCP server is a standalone executable lazily started by first --ai-debug backtest, runs as detached process that is NOT stopped when parent exits
- Q: How should MCP server be explicitly stopped? → A: CLI `--shutdown` flag - Running another instance with `--shutdown` detects existing instance via mutex, signals termination via named EventWaitHandle, existing instance gracefully shuts down

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post-Backtest Debugging Access (Priority: P1)

As an AI agent (or developer using AI tools), I want the MCP server to remain accessible after a backtest completes so that I can query, analyze, and debug the backtest results without needing to re-run the strategy.

**Why this priority**: This is the core problem being solved. Currently the MCP server terminates when the strategy finishes, making post-mortem analysis impossible. This blocks the primary debugging use case for AI agents.

**Independent Test**: Can be fully tested by running a backtest to completion and then issuing MCP tool queries (e.g., GetStateSnapshot, GetEventsByType) to verify the server responds with data from the completed run.

**Acceptance Scenarios**:

1. **Given** a backtest has finished execution, **When** an AI agent sends an MCP tool request, **Then** the MCP server responds with data from the most recent backtest run
2. **Given** the MCP server was started before the backtest, **When** the backtest completes and the strategy is disposed, **Then** the MCP server continues to accept and respond to queries
3. **Given** the MCP server is running, **When** the user initiates shutdown, **Then** the MCP server gracefully terminates and releases all resources

---

### User Story 2 - Single MCP Instance Management (Priority: P2)

As a developer, I want the system to ensure only one MCP server instance runs at any time so that I don't accidentally have multiple servers with conflicting states or resource contention.

**Why this priority**: Multiple MCP instances would cause confusion (which one to query?) and resource conflicts (file locks on SQLite DB). This prevents operational issues.

**Independent Test**: Can be tested by attempting to launch multiple backtests in sequence or parallel and verifying only one MCP server instance exists throughout.

**Acceptance Scenarios**:

1. **Given** an MCP server is already running, **When** a new backtest is started, **Then** the existing MCP server is reused (or refreshed with new DB connection) rather than spawning a second instance
2. **Given** an MCP server is already running, **When** a second launch attempt is made, **Then** the system detects the existing instance and does not create a duplicate
3. **Given** an MCP server is running with old data, **When** a new backtest starts, **Then** the MCP server automatically switches to query the new SQLite database

---

### User Story 3 - Fresh Database on Each Backtest (Priority: P3)

As a developer, I want the SQLite database to be cleared/recreated at the start of each new backtest so that I always analyze fresh data without confusion from previous runs.

**Why this priority**: Stale data from previous runs could lead to incorrect debugging conclusions. A clean slate ensures data integrity per-session.

**Independent Test**: Can be tested by running two backtests in sequence and verifying the database only contains events from the second run.

**Acceptance Scenarios**:

1. **Given** a SQLite database exists from a previous backtest, **When** a new backtest starts, **Then** the old database file is deleted and a new empty database is created
2. **Given** the database cleanup occurs, **When** events are logged during the new backtest, **Then** only events from the current run are present in the database
3. **Given** a cleanup is in progress, **When** the MCP server has an active connection to the old database, **Then** the connection is gracefully closed before deletion

---

### Edge Cases

- What happens if the MCP server process crashes unexpectedly during a backtest? (Expectation: The backtest should continue; MCP restart should reconnect to existing DB)
- What happens if the database file is locked by another process during cleanup? (Expectation: Retry with timeout, then fail gracefully with clear error message)
- What happens if the user requests MCP shutdown while a backtest is still running? (Expectation: MCP should warn but allow shutdown; logging continues to DB, just no queries possible)
- What happens if the database file grows very large (e.g., 1GB+)? (Expectation: Cleanup handles large files efficiently; consider archiving option in future)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: MCP server MUST be implemented as a separate executable project that runs as a detached process independent from the backtest application lifecycle
- **FR-002**: System MUST maintain a maximum of one MCP server instance at any time
- **FR-003**: MCP server MUST remain accessible after backtest completion until explicitly shut down
- **FR-004**: System MUST delete/recreate the SQLite database at the start of each new backtest
- **FR-005**: MCP server MUST automatically reconnect to the current SQLite database when a new backtest starts
- **FR-006**: System MUST auto-start the MCP server on the first --ai-debug backtest run and provide explicit command to stop it independently
- **FR-007**: MCP server MUST use file system watcher to detect database file changes and automatically reconnect without requiring restart
- **FR-008**: Database cleanup MUST gracefully handle active connections before deletion
- **FR-009**: System MUST prevent race conditions between MCP queries and database recreation
- **FR-010**: MCP server startup MUST fail gracefully if another instance is already running
- **FR-011**: MCP server MUST support `--shutdown` CLI argument that signals an existing running instance to terminate via named EventWaitHandle
- **FR-012**: Running MCP server instance MUST listen for shutdown signals and gracefully close DB connections before exiting

### Key Entities

- **MCP Server Executable**: Separate console application project (`StockSharp.AdvancedBacktest.DebugEventLogMcpServer`) that runs as a detached process; handles AI agent queries via stdio transport; survives parent process exit
- **Backtest Run**: Execution of a trading strategy simulation; produces events logged to SQLite; spawns MCP server exe if not already running
- **Event Database**: SQLite file storing backtest events; recreated per-run; queried by MCP server tools
- **Instance Lock**: OS-level named mutex/semaphore to ensure single MCP server instance; acquired on startup, released on shutdown
- **MCP Launcher**: Component in BacktestRunner that detects if MCP exe is running and spawns it as detached process if needed

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: AI agents can query backtest results within 5 seconds of backtest completion without receiving connection errors
- **SC-002**: Running 10 sequential backtests results in exactly 1 MCP server instance active at all times
- **SC-003**: Database queries after a new backtest return only data from the most recent run (0 events from previous runs)
- **SC-004**: MCP server maintains 100% uptime across multiple backtest run/stop cycles in a session
- **SC-005**: Database cleanup completes within 10 seconds regardless of previous database size (up to 1GB)
- **SC-006**: System correctly handles the case where backtest runs while MCP is unavailable, allowing later MCP startup to query the data

## Assumptions

- The MCP server uses stdio transport (standard input/output) as the communication channel with AI agents
- SQLite database is stored in a local filesystem location accessible to both backtest and MCP processes
- The system runs in a single-machine environment (no distributed concerns)
- Database files are not shared across networked machines
- AI agents interact with MCP server through Claude Code or similar tooling that maintains the stdio session
