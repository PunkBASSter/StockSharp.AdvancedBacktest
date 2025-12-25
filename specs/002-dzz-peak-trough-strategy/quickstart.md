# Quickstart: DeltaZz Peak/Trough Breakout Strategy

**Date**: 2025-12-25

## Prerequisites

- .NET 8 SDK installed
- StockSharp history data available (Hydra format)
- Solution built successfully

## Build

```bash
# From repository root
dotnet build StockSharp.AdvancedBacktest.slnx
```

## Run Backtest

### Default (ZigZagBreakout - backward compatible)

```bash
cd StockSharp.AdvancedBacktest.LauncherTemplate
dotnet run
```

### DzzPeakTrough Strategy

```bash
cd StockSharp.AdvancedBacktest.LauncherTemplate
dotnet run -- --strategy DzzPeakTrough
```

### With AI Debug Mode

```bash
dotnet run -- --strategy DzzPeakTrough --ai-debug
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `StockSharp__HistoryPath` | Path to Hydra storage | `.\History` |
| `StockSharp__StorageFormat` | Storage format (Binary/CSV) | `Binary` |

## Configuration

Strategy parameters are configured in the launcher. Default values:

| Parameter | Value | Description |
|-----------|-------|-------------|
| DzzDepth | 5 | Delta parameter (0.5 after /10 conversion) |
| RiskPercentPerTrade | 1% | Portfolio risk per trade |
| MinPositionSize | 0.01 | Minimum order volume |
| MaxPositionSize | 10 | Maximum order volume |

## Run Tests

```bash
# All tests
dotnet test StockSharp.AdvancedBacktest.slnx

# Core unit tests only
dotnet test StockSharp.AdvancedBacktest.Core.Tests/

# Integration tests
dotnet test StockSharp.AdvancedBacktest.Tests/
```

## Key Files

| File | Purpose |
|------|---------|
| `LauncherTemplate/Program.cs` | Entry point with DI setup |
| `LauncherTemplate/Launchers/IStrategyLauncher.cs` | Launcher interface |
| `LauncherTemplate/Launchers/DzzPeakTroughLauncher.cs` | DzzPeakTrough launcher |
| `LauncherTemplate/Strategies/DzzPeakTrough/DzzPeakTroughStrategy.cs` | Strategy implementation |
| `Core/OrderManagement/SignalDeduplicator.cs` | Signal deduplication logic |

## Expected Output

```
=== DzzPeakTrough Strategy Backtest ===

History Path: C:\Users\...\Hydra\Storage
Storage Format: Binary
Period: 2020-01-01 to 2023-12-31
Initial Capital: 10000.00

Strategy Parameters:
  DzzDepth: 5

Starting backtest...

=== Backtest Results ===

Status: SUCCESS
Duration: X.XX seconds

Trading Performance:
  Total Trades: XX
  Win Rate: XX.X%
  ...

=== Report Generated Successfully ===
```

## Troubleshooting

### "No data found" error
- Verify `StockSharp__HistoryPath` points to valid Hydra storage
- Check security ID matches available data (default: BTCUSDT@BNB)

### Strategy not found
- Ensure `--strategy` argument matches exactly: `ZigZagBreakout` or `DzzPeakTrough`

### Build errors
- Update StockSharp submodule: `git submodule update --init --recursive`
- Restore packages: `dotnet restore`
