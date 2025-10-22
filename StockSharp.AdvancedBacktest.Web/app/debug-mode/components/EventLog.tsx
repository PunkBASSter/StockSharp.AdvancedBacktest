'use client';

/**
 * Event log component for debug mode
 * Displays recent events in a scrollable list
 */

import { DebugModeEvent, CandleDataPoint, IndicatorDataPoint, TradeDataPoint, StateDataPoint } from '@/types/debug-mode-events';

interface Props {
  events: DebugModeEvent[];
  maxEvents?: number;
}

const MAX_DISPLAYED_EVENTS = 100;

/**
 * Format timestamp as readable string
 */
function formatTime(timeMs: number): string {
  const date = new Date(timeMs);
  return date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    fractionalSecondDigits: 3,
  });
}

/**
 * Format event data for display
 */
function formatEventData(event: DebugModeEvent): string {
  switch (event.type) {
    case 'candle': {
      const candle = event.data as CandleDataPoint;
      return `O: ${candle.open.toFixed(2)} H: ${candle.high.toFixed(2)} L: ${candle.low.toFixed(2)} C: ${candle.close.toFixed(2)} V: ${candle.volume.toFixed(0)}`;
    }

    case 'trade': {
      const trade = event.data as TradeDataPoint;
      return `${trade.side.toUpperCase()} ${trade.volume.toFixed(2)} @ ${trade.price.toFixed(2)} | PnL: ${trade.pnL >= 0 ? '+' : ''}${trade.pnL.toFixed(2)}`;
    }

    case 'state': {
      const state = event.data as StateDataPoint;
      return `Pos: ${state.position.toFixed(2)} | PnL: ${state.pnL >= 0 ? '+' : ''}${state.pnL.toFixed(2)} | Unrealized: ${state.unrealizedPnL >= 0 ? '+' : ''}${state.unrealizedPnL.toFixed(2)} | State: ${state.processState}`;
    }

    default:
      // indicator_* types
      if (event.type.startsWith('indicator_')) {
        const indicator = event.data as IndicatorDataPoint;
        const name = event.type.replace('indicator_', '');
        return `${name}: ${indicator.value.toFixed(4)}`;
      }
      return JSON.stringify(event.data);
  }
}

/**
 * Get event type color
 */
function getEventTypeColor(type: string): string {
  switch (type) {
    case 'candle':
      return 'text-blue-600 bg-blue-50';
    case 'trade':
      return 'text-green-600 bg-green-50';
    case 'state':
      return 'text-purple-600 bg-purple-50';
    default:
      // indicator_* types
      if (type.startsWith('indicator_')) {
        return 'text-orange-600 bg-orange-50';
      }
      return 'text-gray-600 bg-gray-50';
  }
}

export default function EventLog({ events, maxEvents = MAX_DISPLAYED_EVENTS }: Props) {
  // Show most recent events
  const recentEvents = events.slice(-maxEvents).reverse();

  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-sm">
      <div className="px-4 py-3 border-b border-gray-200">
        <h3 className="text-lg font-semibold text-gray-900">Event Log</h3>
        <p className="text-xs text-gray-500 mt-1">
          Showing last {Math.min(maxEvents, events.length)} of {events.length.toLocaleString()} events
        </p>
      </div>

      <div className="overflow-y-auto max-h-[400px]">
        {recentEvents.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            <p>No events yet. Waiting for data...</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {recentEvents.map((event, index) => {
              const timeMs = typeof event.data === 'object' && 'time' in event.data
                ? (event.data as { time: number }).time
                : 0;

              const sequenceNumber = typeof event.data === 'object' && 'sequenceNumber' in event.data
                ? (event.data as { sequenceNumber?: number }).sequenceNumber
                : undefined;

              return (
                <div
                  key={`${event.type}-${timeMs}-${index}`}
                  className="px-4 py-2 hover:bg-gray-50 transition-colors"
                >
                  <div className="flex items-start gap-3">
                    {/* Event Type Badge */}
                    <span
                      className={`inline-flex items-center px-2 py-1 rounded text-xs font-medium ${getEventTypeColor(event.type)}`}
                    >
                      {event.type}
                    </span>

                    {/* Event Details */}
                    <div className="flex-1 min-w-0">
                      <p className="text-sm text-gray-900 font-mono truncate">
                        {formatEventData(event)}
                      </p>
                      <div className="flex items-center gap-4 mt-1">
                        <p className="text-xs text-gray-500">
                          {formatTime(timeMs)}
                        </p>
                        {sequenceNumber !== undefined && (
                          <p className="text-xs text-gray-400">
                            Seq: {sequenceNumber}
                          </p>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
