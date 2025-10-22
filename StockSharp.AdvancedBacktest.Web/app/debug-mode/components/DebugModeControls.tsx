'use client';

/**
 * Control panel for debug mode real-time visualization
 * Provides pause/resume, reset, and file path controls
 */

import { useEffect } from 'react';

interface Props {
  isPolling: boolean;
  onTogglePoll: () => void;
  onReset: () => void;
  filePath: string;
  onFilePathChange: (path: string) => void;
  stats: {
    totalEvents: number;
    candles: number;
    indicators: number;
    trades: number;
  };
  error: string | null;
}

export default function DebugModeControls({
  isPolling,
  onTogglePoll,
  onReset,
  filePath,
  onFilePathChange,
  stats,
  error,
}: Props) {
  /**
   * Keyboard shortcuts
   * Space: Pause/Resume
   * R: Reset
   */
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      // Ignore if user is typing in input field
      if (event.target instanceof HTMLInputElement) return;

      switch (event.key) {
        case ' ':
          event.preventDefault();
          onTogglePoll();
          break;
        case 'r':
        case 'R':
          event.preventDefault();
          onReset();
          break;
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [onTogglePoll, onReset]);

  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-sm p-4 space-y-4">
      {/* File Path Input */}
      <div className="space-y-2">
        <label htmlFor="file-path" className="block text-sm font-medium text-gray-700">
          JSONL File Path
        </label>
        <input
          id="file-path"
          type="text"
          value={filePath}
          onChange={(e) => onFilePathChange(e.target.value)}
          className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          placeholder="/debug-mode/latest.jsonl"
        />
      </div>

      {/* Control Buttons */}
      <div className="flex items-center gap-3">
        {/* Status Indicator */}
        <div className="flex items-center gap-2">
          <div
            className={`w-3 h-3 rounded-full ${
              error
                ? 'bg-red-500'
                : isPolling
                  ? 'bg-green-500 animate-pulse'
                  : 'bg-yellow-500'
            }`}
          />
          <span className="text-sm font-medium text-gray-700">
            {error ? 'Error' : isPolling ? 'Polling' : 'Paused'}
          </span>
        </div>

        {/* Pause/Resume Button */}
        <button
          onClick={onTogglePoll}
          className={`px-4 py-2 rounded-md font-medium transition-colors ${
            isPolling
              ? 'bg-yellow-500 hover:bg-yellow-600 text-white'
              : 'bg-green-500 hover:bg-green-600 text-white'
          }`}
        >
          {isPolling ? 'Pause' : 'Resume'}
        </button>

        {/* Reset Button */}
        <button
          onClick={onReset}
          className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md font-medium transition-colors"
        >
          Reset
        </button>

        {/* Keyboard Shortcuts Help */}
        <div className="ml-auto text-xs text-gray-500">
          <kbd className="px-2 py-1 bg-gray-100 border border-gray-300 rounded">Space</kbd> Pause/Resume
          <span className="mx-2">|</span>
          <kbd className="px-2 py-1 bg-gray-100 border border-gray-300 rounded">R</kbd> Reset
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="bg-red-50 border border-red-200 rounded-md p-3">
          <p className="text-sm text-red-800 font-medium">Error</p>
          <p className="text-sm text-red-600 mt-1">{error}</p>
        </div>
      )}

      {/* Event Statistics */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pt-4 border-t border-gray-200">
        <div className="text-center">
          <p className="text-2xl font-bold text-gray-900">{stats.totalEvents.toLocaleString()}</p>
          <p className="text-xs text-gray-500 uppercase tracking-wide">Total Events</p>
        </div>
        <div className="text-center">
          <p className="text-2xl font-bold text-blue-600">{stats.candles.toLocaleString()}</p>
          <p className="text-xs text-gray-500 uppercase tracking-wide">Candles</p>
        </div>
        <div className="text-center">
          <p className="text-2xl font-bold text-purple-600">{stats.indicators.toLocaleString()}</p>
          <p className="text-xs text-gray-500 uppercase tracking-wide">Indicators</p>
        </div>
        <div className="text-center">
          <p className="text-2xl font-bold text-green-600">{stats.trades.toLocaleString()}</p>
          <p className="text-xs text-gray-500 uppercase tracking-wide">Trades</p>
        </div>
      </div>
    </div>
  );
}
