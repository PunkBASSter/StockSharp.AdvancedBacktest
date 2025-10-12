# History Data Setup Guide

This guide explains how to set up historical market data for backtesting with StockSharp Advanced Backtest.

## Overview

The backtest system uses **StockSharp Hydra** to download and manage historical market data. Data is stored locally in binary format for efficient access during backtesting.

## Prerequisites

- **StockSharp Hydra** installed (download from https://stocksharp.com/products/hydra/)
- Sufficient disk space for historical data (varies by security and timeframe)
- Internet connection for downloading data

## Data Storage Location

### Default Location
Windows: `C:\Users\<Username>\Documents\StockSharp\Hydra\Storage\`

### Your Current Configuration
The test configuration uses: `C:/Users/Andrew/OneDrive/Документы/StockSharp/Hydra/Storage/`

**Note**: If using OneDrive, ensure the folder is set to "Always keep on this device" for offline access.

## Directory Structure

```
Storage/
├── B/
│   └── BTCUSDT@BNB/
│       ├── 2017_08_01/
│       │   └── candles_TimeFrameCandle_1.00-00-00.bin
│       ├── 2017_08_02/
│       └── ...
├── E/
│   └── ETHUSDT@BNB/
│       └── ...
└── ...
```

### Format Explanation
- First letter directories (B, E, etc.) organize securities alphabetically
- Security ID format: `<Symbol>@<Exchange>` (e.g., `BTCUSDT@BNB` for Binance BTCUSDT)
- Date directories: `YYYY_MM_DD` format
- Binary files: `candles_TimeFrameCandle_<TimeSpan>.bin`

## Downloading Data with Hydra

### Step 1: Launch Hydra
1. Open StockSharp Hydra application
2. Configure the storage path in Settings if needed

### Step 2: Add Data Source
1. Go to **Sources** tab
2. Add a connector (e.g., Binance, Interactive Brokers, Kraken)
3. Configure connection parameters (API keys if required)

### Step 3: Select Securities
1. In the **Securities** tab, search for desired securities
2. Add securities to download list (e.g., BTCUSDT@BNB)

### Step 4: Configure Download Task
1. Go to **Tasks** tab
2. Create new task
3. Select:
   - **Source**: Your connector
   - **Securities**: Selected securities
   - **Data Type**: Candles
   - **Timeframe**: e.g., 1 day, 1 hour
   - **Date Range**: Historical period to download
4. Start the download task

### Step 5: Verify Downloaded Data
1. Check the Storage folder for new data
2. Use the validation tool: `dotnet run --validate-data`

## Supported Data Formats

### Binary (Recommended)
- Format: StockSharp binary format (`.bin` files)
- Advantages: Fast access, efficient storage
- Default format used by the system

### CSV (Alternative)
- Format: Comma-separated values
- Advantages: Human-readable, can be manually edited
- Requires additional configuration

## Configuration

### Backtest Configuration File
Edit `ConfigFiles/test-backtest-btcusdt.json`:

```json
{
  "HistoryPath": "C:/Users/Andrew/OneDrive/Документы/StockSharp/Hydra/Storage/",
  "Securities": ["BTCUSDT@BNB"],
  "TimeFrames": ["1d"],
  ...
}
```

### Key Parameters
- **HistoryPath**: Absolute path to Hydra storage directory
- **Securities**: List of security IDs to test (format: `Symbol@Exchange`)
- **TimeFrames**: Supported values: `1s`, `1m`, `5m`, `15m`, `1h`, `4h`, `1d`, `1w`

## Validating Data

### Quick Validation (CLI Tool)
```bash
dotnet run --project StockSharp.AdvancedBacktest.LauncherTemplate -- --validate-data
```

This command:
- Checks if the history path exists and is accessible
- Lists available securities in storage
- Verifies data availability for configured securities and timeframes
- Reports date ranges and gaps

### Expected Output
```
=== History Data Validation Report ===
Path: C:/Users/Andrew/OneDrive/.../Storage/
Status: ✓ SUCCESS

Found 15 securities in storage

Validating security: BTCUSDT@BNB
  ✓ TimeFrame 1d: 1250 dates available (2020-01-01 to 2024-01-31)
```

### Automated Testing (xUnit)
Run integration tests to validate data access:

```bash
# All tests
dotnet test

# Only integration tests
dotnet test --filter "Category=Integration"

# Skip long-running E2E tests
dotnet test --filter "Category!=E2E"
```

Tests include:
- `HistoryDataValidatorTests` - Validates data access and reporting
- `BacktestRunnerIntegrationTests` - Tests backtest configuration
- `BacktestPipelineIntegrationTests` - E2E pipeline tests (requires real data)

## Troubleshooting

### Error: "History path not found"

**Cause**: Directory doesn't exist or is inaccessible

**Solutions**:
1. Verify the path in configuration matches Hydra storage location
2. Check folder permissions
3. If using OneDrive:
   - Ensure OneDrive is running and synced
   - Right-click folder → "Always keep on this device"
   - Wait for sync to complete

### Error: "No data found for security"

**Cause**: Data not downloaded or incorrect security ID

**Solutions**:
1. Verify security ID format: `SYMBOL@EXCHANGE` (case-sensitive)
2. Check if data exists in Hydra storage folder
3. Re-download data using Hydra
4. Confirm date range in configuration matches available data

### Error: "No data for timeframe"

**Cause**: Specific timeframe not available

**Solutions**:
1. Check which timeframes are available using `--validate-data`
2. Download the required timeframe using Hydra
3. Update configuration to use available timeframe

### Warning: "OneDrive path not immediately accessible"

**Cause**: OneDrive syncing delay

**Solutions**:
1. Wait for OneDrive to sync (system retries automatically)
2. Set folder to "Always keep on this device" in OneDrive settings
3. Consider moving data to local drive for faster access

### Performance Issues

**Symptom**: Slow data loading during backtest

**Solutions**:
1. **Move data to local SSD**: Copy storage folder from OneDrive to local drive
2. **Reduce data scope**: Use shorter date ranges or fewer securities
3. **Increase RAM**: Large datasets benefit from more available memory
4. **Use parallel workers**: Increase `ParallelWorkers` in configuration

## Best Practices

### For Development/Testing
- Use shorter date ranges (1-2 years)
- Test with 1-2 securities initially
- Use daily candles for faster tests
- Keep data on local SSD if possible

### For Production Backtests
- Download complete historical data for all required securities
- Verify data completeness before long-running backtests
- Use automated Hydra tasks to keep data updated
- Backup storage folder regularly

### Data Management
1. **Regular Updates**: Schedule Hydra tasks to download new data daily
2. **Version Control**: Don't commit binary data files to Git (already in `.gitignore`)
3. **Backup**: Keep backup of storage folder, especially for custom data
4. **Cleanup**: Remove old or unused securities to save disk space

## Security ID Reference

### Common Exchanges
- Binance: `BTCUSDT@BNB`, `ETHUSDT@BNB`
- Interactive Brokers: `AAPL@NASDAQ`, `SPY@NYSE`
- Kraken: `BTCUSD@KRAKEN`, `ETHUSD@KRAKEN`

### Finding Security IDs
1. Open Hydra
2. Go to Securities tab
3. Search for symbol
4. Copy exact ID shown (including @ and exchange code)

## Getting Help

### Documentation
- StockSharp Hydra: https://doc.stocksharp.com/topics/hydra.html
- Storage Formats: https://doc.stocksharp.com/topics/api/market_data_storage.html

### Common Issues
- Check `--validate-data` output for specific errors
- Review Hydra logs for download errors
- Verify network connectivity for remote data sources

### Support
- StockSharp Forum: https://stocksharp.com/forum/
- GitHub Issues: https://github.com/PunkBASSter/StockSharp.AdvancedBacktest/issues
