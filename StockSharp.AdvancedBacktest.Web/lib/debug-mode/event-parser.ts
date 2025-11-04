/**
 * JSONL event parser for debug mode streaming
 * Handles incremental parsing with partial line buffering
 */

import { DebugModeEvent, isDebugModeEvent } from '@/types/debug-mode-events';

/**
 * Parser state for incremental JSONL parsing
 */
export class JSONLParser {
  private partialLine: string = '';

  /**
   * Parse JSONL text chunk, handling partial lines at boundaries
   *
   * @param text - Text chunk from HTTP response (may contain partial lines)
   * @returns Array of parsed DebugModeEvents
   *
   * @example
   * const parser = new JSONLParser();
   *
   * // First chunk (ends mid-line)
   * const events1 = parser.parse('{"type":"candle","data":{...}}\n{"type":"tr');
   * // Returns 1 event, buffers '{"type":"tr'
   *
   * // Second chunk (completes previous line)
   * const events2 = parser.parse('ade","data":{...}}\n');
   * // Returns 1 event (completes buffered line)
   */
  parse(text: string): DebugModeEvent[] {
    // Prepend any incomplete line from previous chunk
    const fullText = this.partialLine + text;
    const lines = fullText.split('\n');

    // Last line might be incomplete (no trailing newline)
    // Store it for next chunk
    this.partialLine = lines.pop() || '';

    // Parse complete lines
    const events: DebugModeEvent[] = [];

    for (const line of lines) {
      const trimmedLine = line.trim();
      if (!trimmedLine) continue; // Skip empty lines

      try {
        const parsed = JSON.parse(trimmedLine);

        // Validate event structure
        if (isDebugModeEvent(parsed)) {
          events.push(parsed);
        } else {
          console.warn('Invalid event structure:', parsed);
        }
      } catch (error) {
        console.error('Failed to parse JSONL line:', trimmedLine, error);
      }
    }

    return events;
  }

  /**
   * Reset parser state (clears partial line buffer)
   * Call this when starting to read a new file
   */
  reset(): void {
    this.partialLine = '';
  }

  /**
   * Get the current partial line buffer
   * Useful for debugging parser state
   */
  getPartialLine(): string {
    return this.partialLine;
  }
}

/**
 * Parse Content-Range header to extract next byte position
 *
 * @param contentRange - Content-Range header value (e.g., "bytes 1234-5678/5678")
 * @returns Next byte position to read, or null if parsing fails
 *
 * @example
 * const nextByte = parseContentRange('bytes 1234-5678/5678');
 * // Returns 5679 (5678 + 1)
 */
export function parseContentRange(contentRange: string | null): number | null {
  if (!contentRange) return null;

  // Format: "bytes start-end/total"
  const match = contentRange.match(/bytes \d+-(\d+)\/\d+/);
  if (!match) return null;

  const lastByteRead = parseInt(match[1], 10);
  return lastByteRead + 1; // Next byte position
}

/**
 * Calculate event statistics from event array
 *
 * @param events - Array of DebugModeEvents
 * @returns Statistics object with event counts by type
 */
export function calculateEventStats(events: DebugModeEvent[]): {
  totalEvents: number;
  candles: number;
  indicators: number;
  trades: number;
  states: number;
  indicatorTypes: Set<string>;
} {
  const stats = {
    totalEvents: events.length,
    candles: 0,
    indicators: 0,
    trades: 0,
    states: 0,
    indicatorTypes: new Set<string>(),
  };

  for (const event of events) {
    switch (event.type) {
      case 'candle':
        stats.candles++;
        break;
      case 'trade':
        stats.trades++;
        break;
      case 'state':
        stats.states++;
        break;
      default:
        // indicator_* types
        if (event.type.startsWith('indicator_')) {
          stats.indicators++;
          stats.indicatorTypes.add(event.type);
        }
        break;
    }
  }

  return stats;
}
