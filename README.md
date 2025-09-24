# StockSharp.AdvancedBacktest

Cross-platform customization of StockSharp allowing to optimize combinations of symbols, timeframes and strategy modules; flexibly export and save optimization data; build interactive backtest reports; easily launch strategies with settings imports.

## Setup

1. After cloning the current repo, clone the StockSharp repository.
2. Create a symlink to the cloned StockSharp repository:

```powershell
New-Item -ItemType Junction -Path ".\StockSharp" -Target "..\StockSharpFork"
```

3. Build the solution in the root directory.

```powershell
dotnet build StockSharp.AdvancedBacktest.sln
```
