'use client';

/**
 * MANUAL TESTING INSTRUCTIONS:
 *
 * 1. Start a backtest with DebugExporter configured
 * 2. Copy output JSONL file to /public/debug-mode/latest.jsonl
 * 3. Navigate to http://localhost:3000/debug-mode
 * 4. Verify charts update as backtest runs
 * 5. Test pause/resume controls (Space key or button)
 * 6. Test reset button (R key or button)
 * 7. Open browser Network tab, verify Range headers are being sent
 * 8. Test file rotation (when backend creates _001.jsonl)
 * 9. Test error handling (invalid file path)
 * 10. Test responsive layout on mobile/tablet
 *
 * Expected Behavior:
 * - File should poll every 500ms using Range headers
 * - Charts should update smoothly without flickering
 * - Pause should stop polling immediately
 * - Reset should clear all events and restart from byte 0
 * - No duplicate events should appear
 * - Browser console should be error-free
 */

import { useRealtimeUpdates } from '@/hooks/debug-mode/useRealtimeUpdates';
import { calculateEventStats } from '@/lib/debug-mode/event-parser';
import { useMemo, useState } from 'react';
import DebugModeChart from './components/DebugModeChart';
import DebugModeControls from './components/DebugModeControls';
import EventLog from './components/EventLog';

const DEFAULT_FILE_PATH = '/debug-mode/latest.jsonl';

export default function DebugModePage() {
    const [filePath, setFilePath] = useState<string>(DEFAULT_FILE_PATH);

    // Use real-time updates hook
    const { events, isPolling, setIsPolling, error, stats: pollStats, reset } = useRealtimeUpdates(filePath);

    // Calculate event statistics
    const eventStats = useMemo(() => {
        const stats = calculateEventStats(events);
        console.log('[DebugModePage] Event stats:', stats);
        console.log('[DebugModePage] Total events:', events.length);
        if (events.length > 0) {
            const indicatorEvents = events.filter(e => e.type.startsWith('indicator_'));
            console.log('[DebugModePage] Indicator events sample:', indicatorEvents.slice(0, 3));
        }
        return stats;
    }, [events]);

    // Toggle polling
    const handleTogglePoll = () => {
        setIsPolling(!isPolling);
    };

    // Reset handler
    const handleReset = () => {
        reset();
    };

    // File path change handler
    const handleFilePathChange = (newPath: string) => {
        setFilePath(newPath);
    };

    return (
        <main className="min-h-screen bg-gray-50">
            {/* Header */}
            <header className="bg-white border-b border-gray-200 shadow-sm">
                <div className="max-w-7xl mx-auto px-4 py-6 sm:px-6 lg:px-8">
                    <div className="flex items-center justify-between">
                        <div>
                            <h1 className="text-3xl font-bold text-gray-900">Backtest Debug Mode</h1>
                            <p className="text-sm text-gray-500 mt-1">
                                Real-time visualization of backtest execution
                            </p>
                        </div>
                        <div className="text-right">
                            <p className="text-sm text-gray-500">Polling Interval: 500ms</p>
                            <p className="text-sm text-gray-500">
                                Bytes Read: {pollStats.bytesRead.toLocaleString()}
                            </p>
                        </div>
                    </div>
                </div>
            </header>

            {/* Main Content */}
            <div className="max-w-7xl mx-auto px-4 py-8 sm:px-6 lg:px-8">
                <div className="space-y-6">
                    {/* Controls */}
                    <DebugModeControls
                        isPolling={isPolling}
                        onTogglePoll={handleTogglePoll}
                        onReset={handleReset}
                        filePath={filePath}
                        onFilePathChange={handleFilePathChange}
                        stats={{
                            totalEvents: eventStats.totalEvents,
                            candles: eventStats.candles,
                            indicators: eventStats.indicators,
                            trades: eventStats.trades,
                        }}
                        error={error}
                    />

                    {/* Chart */}
                    <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-6">
                        <h2 className="text-xl font-semibold text-gray-900 mb-4">
                            Real-Time Chart
                        </h2>
                        {events.length === 0 ? (
                            <div className="flex items-center justify-center h-[400px] md:h-[600px] bg-gray-50 rounded-lg">
                                <div className="text-center">
                                    <div className="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mb-4"></div>
                                    <p className="text-gray-500">Waiting for data...</p>
                                    <p className="text-sm text-gray-400 mt-2">
                                        Make sure the JSONL file exists at: <code className="bg-gray-100 px-2 py-1 rounded">{filePath}</code>
                                    </p>
                                </div>
                            </div>
                        ) : (
                            <DebugModeChart events={events} />
                        )}
                    </div>

                    {/* Event Log */}
                    <EventLog events={events} />

                    {/* Footer Stats */}
                    <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-6">
                        <h3 className="text-lg font-semibold text-gray-900 mb-4">Statistics</h3>
                        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4">
                            <div>
                                <p className="text-sm text-gray-500 uppercase tracking-wide">Total Events</p>
                                <p className="text-2xl font-bold text-gray-900 mt-1">
                                    {eventStats.totalEvents.toLocaleString()}
                                </p>
                            </div>
                            <div>
                                <p className="text-sm text-gray-500 uppercase tracking-wide">Candles</p>
                                <p className="text-2xl font-bold text-blue-600 mt-1">
                                    {eventStats.candles.toLocaleString()}
                                </p>
                            </div>
                            <div>
                                <p className="text-sm text-gray-500 uppercase tracking-wide">Indicators</p>
                                <p className="text-2xl font-bold text-purple-600 mt-1">
                                    {eventStats.indicators.toLocaleString()}
                                </p>
                            </div>
                            <div>
                                <p className="text-sm text-gray-500 uppercase tracking-wide">Trades</p>
                                <p className="text-2xl font-bold text-green-600 mt-1">
                                    {eventStats.trades.toLocaleString()}
                                </p>
                            </div>
                            <div>
                                <p className="text-sm text-gray-500 uppercase tracking-wide">States</p>
                                <p className="text-2xl font-bold text-orange-600 mt-1">
                                    {eventStats.states.toLocaleString()}
                                </p>
                            </div>
                        </div>

                        {/* Indicator Types */}
                        {eventStats.indicatorTypes.size > 0 && (
                            <div className="mt-6">
                                <p className="text-sm text-gray-500 uppercase tracking-wide mb-2">
                                    Indicator Types ({eventStats.indicatorTypes.size})
                                </p>
                                <div className="flex flex-wrap gap-2">
                                    {Array.from(eventStats.indicatorTypes).map((type) => (
                                        <span
                                            key={type}
                                            className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-purple-100 text-purple-800"
                                        >
                                            {type.replace('indicator_', '')}
                                        </span>
                                    ))}
                                </div>
                            </div>
                        )}

                        {/* Last Poll Info */}
                        {pollStats.lastPollTime > 0 && (
                            <div className="mt-6 pt-6 border-t border-gray-200">
                                <p className="text-sm text-gray-500">
                                    Last poll: {new Date(pollStats.lastPollTime).toLocaleTimeString()}
                                </p>
                                <p className="text-sm text-gray-500">
                                    Events in last poll: {pollStats.eventCount.toLocaleString()}
                                </p>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </main>
    );
}
