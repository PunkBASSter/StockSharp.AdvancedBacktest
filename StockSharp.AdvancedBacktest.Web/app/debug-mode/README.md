# Debug Mode - Real-Time Backtest Visualization

This directory contains the frontend implementation for real-time visualization of StockSharp backtest execution.

## Overview

The debug mode allows developers to watch candles, indicators, and trades update live as a strategy processes historical data, instead of waiting for backtest completion.

### Architecture

```
C# Backend (DebugModeExporter)
         ↓
   JSONL Files (append-only)
         ↓
   HTTP Range Polling (500ms)
         ↓
Frontend (Next.js + Lightweight Charts)
```

## Features

- **Real-time chart updates** using Lightweight Charts v4
- **File polling** with HTTP Range headers for efficient incremental reads
- **Event types**: Candles, Indicators, Trades, Strategy State
- **Pause/Resume** controls with keyboard shortcuts
- **Reset** functionality to restart from beginning
- **Event log** showing recent debug events
- **Statistics** dashboard with event counts

## File Structure

```
app/debug-mode/
├── page.tsx                          Main debug mode page
├── components/
│   ├── DebugModeChart.tsx            Real-time chart component
│   ├── DebugModeControls.tsx         Control panel (pause/resume/reset)
│   └── EventLog.tsx                  Event list view (last 100 events)
├── README.md                         This file

hooks/debug-mode/
└── useRealtimeUpdates.ts             File polling hook with Range headers

lib/debug-mode/
├── event-parser.ts                   JSONL parsing utilities
└── file-rotation-handler.ts          File rotation detection

types/
└── debug-mode-events.ts              TypeScript type definitions

public/debug-mode/
└── .gitkeep                          JSONL files go here
```

## Usage

### 1. Start a Backtest with Debug Mode

Configure the C# backend to export JSONL files (see backend `DebugMode/` folder documentation).

### 2. Copy JSONL Files to Public Directory

```bash
# Copy backend output to frontend public folder
cp path/to/backtest/output/latest.jsonl public/debug-mode/latest.jsonl
```

### 3. Navigate to Debug Mode Page

```
http://localhost:3000/debug-mode
```

### 4. Controls

- **Pause/Resume**: Click button or press `Space` key
- **Reset**: Click button or press `R` key
- **File Path**: Edit the input field to load different JSONL files

## Technical Details

### JSONL File Format

Each line in the JSONL file is a JSON object representing one event:

```jsonl
{"type":"candle","data":{"time":1729555200000,"open":50000,"high":50100,"low":49950,"close":50050,"volume":1250.5}}
{"type":"indicator_SMA","data":{"time":1729555200000,"value":49980.5}}
{"type":"trade","data":{"time":1729555200000,"price":50050,"volume":0.01,"side":"buy","pnL":0}}
{"type":"state","data":{"time":1729555200000,"position":0.01,"pnL":50,"unrealizedPnL":0,"processState":"Running"}}
```

### HTTP Range Headers

The hook uses HTTP Range headers to read only new data:

```
Request:  Range: bytes=1234-
Response: Content-Range: bytes 1234-5678/5678
```

This allows efficient polling without re-reading the entire file.

### Performance Optimizations

1. **Time-based buffering**: Backend flushes every 500ms (matches polling interval)
2. **Incremental parsing**: Parse only new bytes, not entire file
3. **Chart update throttling**: `requestAnimationFrame` for smooth 60fps
4. **Memory limits**: Keep max 10,000 candles in memory
5. **Batch updates**: Accumulate events and update chart in single animation frame

### File Rotation

Backend rotates files when they reach 10MB:
- `latest.jsonl` → `latest_001.jsonl` → `latest_002.jsonl`

Frontend can detect rotation and switch to next file (see `file-rotation-handler.ts`).

## Development

### Running Locally

```bash
npm run dev
```

Navigate to `http://localhost:3000/debug-mode`

### Building

```bash
npm run build
```

### Type Checking

```bash
npx tsc --noEmit
```

## Testing

See testing instructions in `page.tsx` header comments.

### Manual Test Checklist

1. ✅ File polls every 500ms (verify in Network tab)
2. ✅ Charts update smoothly without flickering
3. ✅ Pause stops polling immediately
4. ✅ Resume restarts polling
5. ✅ Reset clears events and restarts from byte 0
6. ✅ No duplicate events appear
7. ✅ Trade markers show on chart
8. ✅ Indicators render as separate line series
9. ✅ Event log shows recent events
10. ✅ Statistics update correctly

### Known Limitations

- **Server must support Range headers**: If server returns 200 instead of 206, falls back to full file read (less efficient)
- **No playback controls**: Frontend displays events as they arrive; backend controls timing
- **File rotation**: Auto-discovery not yet implemented (manual path change required)

## Troubleshooting

### Charts not updating

1. Check browser Network tab for 404 errors
2. Verify JSONL file exists at specified path
3. Check browser console for parsing errors
4. Verify backend is flushing data (500ms interval)

### Duplicate events

- Reset state when switching files
- Verify byte tracking is correct (Content-Range header parsing)

### Performance issues

- Limit history to 10k candles (already implemented)
- Reduce polling interval if needed
- Check for JSON parsing errors in console

## Related Files

- **Backend**: `StockSharp.AdvancedBacktest/DebugMode/`
- **TRD**: `docs/5_TRD_DebugMode.md`
- **Type Definitions**: `types/debug-mode-events.ts`

## Future Enhancements

- [ ] WebSocket support for real-time streaming (no polling)
- [ ] Playback speed controls
- [ ] Step forward/backward functionality
- [ ] Auto-discovery of rotated files
- [ ] Export current view as screenshot
- [ ] Bookmark specific timestamps
