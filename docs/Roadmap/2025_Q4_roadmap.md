# 2025 Q4 Roadmap

## 2. Add .claude/CLAUDE.md definitions
- For each sub-project (e.g., StockSharp.AdvancedBacktest, StockSharp.AdvancedBacktest.LauncherTemplate), create a CLAUDE.md file defining the main agents and their responsibilities.
- Define the roles of Product Manager Agent, Tech Lead Agent, Backend Implementer Agent, QA Tester Agent, and Documentation Specialist Agent and map them to specific subagents.
- Ensure clear instructions for each agent in their definitions.

## 3. Strategy integration tests for ZigZagBreakoutStrategy in backend code:
- Determine a set of cases (effective dates, markets, instruments) to run the ZigZagBreakoutStrategy in backtest mode with known expected outcomes.
- Implement tests for both debug mode (only for backend events export) and standard backtest flow (asserting exported data files).
- Example BTCUSDT@BNB after new DateTimeOffset(2020, 2, 06, 9, 0, 0, TimeSpan.Zero) - there were no protective orders placed.
- Example BTCUSDT@BNB after new DateTimeOffset(2020, 4, 29, 1, 0, 0, TimeSpan.Zero) - big candle with both local peak and trough. - Challenging the existing DeltaZigZag logic.

## 4. LLM-agent-friendly events logging of backtest execution:
- Enhance logging in backtest engine to produce structured, LLM-agent-friendly logs. Requires full PRD and TRD. The idea is to enable LLM agents to analyze backtest runs and identify issues or optimization opportunities. Potential implementation could involve a DB engine with MCP as an agent-friendly interface (to save tokens, by fetching only relevant events).

## 5. Enhance backtesting engine to support multiple timeframes:

Main Goal: Increase modeling accuracy for strategies that operate with pending orders and rely on price levels, to enhance order/tp/sl execution behavior during backtests.

- Introduce maintenance timeframe - the smallest timeframe for more precise order/position behavior modeling. Make it configurable per strategy launch. This timeframe data is not visible in UI or exports.
- Standard timeframes for indicator calculations - retain existing behavior. The smallest standard timeframe will be used for indicators and export.

### Affected Components
- @StockSharp.AdvancedBacktest
    - @StockSharp.AdvancedBacktest\Backtest
    - @StockSharp.AdvancedBacktest\Strategies
    - @StockSharp.AdvancedBacktest\Export --Not exporting small
    - @StockSharp.AdvancedBacktest\DebugMode
    - TBD: Other components?

## 6. Bug Fixes and Improvements for ZigZagBreakoutStrategy:

@StockSharp.AdvancedBacktest\Strategies\ZigZagBreakoutStrategy.cs - is the target strategy for improvements.

Fix existing bugs related to order execution and position management.
Observed Issues:
- Position not closed when TP/SL hit during backtest (same candle bug: when open price break happens on the same candle where TP/SL is supposed to be triggered).
- Opened position that was not closed by TP/SL remains open indefinitely until the backtest stops.
- When a strategy's balance goes negative, it continues to place orders instead of halting (?).
- A bug with double order placement on nearby zigzag peaks. Expected: only one order on the last peak, the previous should be cancelled.
- Contraversial logic of DeltaZigZag indicator - sometimes it needs a fully vertical line if a big candle with both local peak and trough appears (no exact rules for it yet, not sure if vertical lines are supported by FE charts lib).

## 7. Refactoring of StockSharp.AdvancedBacktest project:
- Extract infrastructure components (data management, exporting, logging) into StockSharp.AdvancedBacktest.Infrastructure to enforce separation of concerns and make Strategy and modules more business-focused without infrastructure concerns.
