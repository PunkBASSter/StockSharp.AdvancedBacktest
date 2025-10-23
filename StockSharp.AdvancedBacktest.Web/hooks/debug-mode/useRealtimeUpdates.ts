/**
 * React hook for real-time JSONL file polling using HTTP Range headers
 * Implements incremental file reading for debug mode event streaming
 */

import { JSONLParser, parseContentRange } from '@/lib/debug-mode/event-parser';
import { DebugModeEvent } from '@/types/debug-mode-events';
import { useCallback, useEffect, useRef, useState } from 'react';

export interface RealtimeUpdateStats {
    bytesRead: number;
    eventCount: number;
    lastPollTime: number;
}

export interface UseRealtimeUpdatesResult {
    events: DebugModeEvent[];
    isPolling: boolean;
    setIsPolling: (polling: boolean) => void;
    error: string | null;
    stats: RealtimeUpdateStats;
    reset: () => void;
}

const POLL_INTERVAL_MS = 500; // Match backend flush interval
const MAX_RETRY_ATTEMPTS = 3;
const RETRY_BACKOFF_MS = 1000; // Initial backoff, doubles each retry

/**
 * Hook for real-time JSONL file updates using HTTP Range headers
 *
 * @param filePath - Path to JSONL file (e.g., '/debug-mode/latest.jsonl')
 * @returns Object with events array, polling controls, and statistics
 *
 * @example
 * const { events, isPolling, setIsPolling, reset, stats, error } = useRealtimeUpdates('/debug-mode/latest.jsonl');
 *
 * // Pause polling
 * setIsPolling(false);
 *
 * // Resume polling
 * setIsPolling(true);
 *
 * // Reset (clear events and start from beginning)
 * reset();
 */
export function useRealtimeUpdates(filePath: string): UseRealtimeUpdatesResult {
    const [events, setEvents] = useState<DebugModeEvent[]>([]);
    const [isPolling, setIsPolling] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const [stats, setStats] = useState<RealtimeUpdateStats>({
        bytesRead: 0,
        eventCount: 0,
        lastPollTime: 0,
    });

    // Use refs to maintain state across polling cycles
    const lastByteReadRef = useRef<number>(0);
    const parserRef = useRef<JSONLParser>(new JSONLParser());
    const abortControllerRef = useRef<AbortController | null>(null);
    const retryCountRef = useRef<number>(0);

    /**
     * Reset all state and start reading from beginning of file
     */
    const reset = useCallback(() => {
        // Cancel any pending request
        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
        }

        // Reset state
        setEvents([]);
        setError(null);
        lastByteReadRef.current = 0;
        parserRef.current.reset();
        retryCountRef.current = 0;
        setStats({
            bytesRead: 0,
            eventCount: 0,
            lastPollTime: 0,
        });
    }, []);

    /**
     * Fetch new data from file using Range header
     */
    const pollFile = useCallback(async () => {
        if (!isPolling) return;

        // Create new AbortController for this request
        const abortController = new AbortController();
        abortControllerRef.current = abortController;

        try {
            const rangeStart = lastByteReadRef.current;
            const headers: HeadersInit = {
                'Range': `bytes=${rangeStart}-`,
            };

            const response = await fetch(filePath, {
                headers,
                signal: abortController.signal,
            });

            // Handle different response codes
            if (response.status === 206) {
                // Partial Content - success, file has grown
                const text = await response.text();
                const contentRange = response.headers.get('Content-Range');
                const nextByte = parseContentRange(contentRange);

                if (nextByte !== null) {
                    lastByteReadRef.current = nextByte;
                }

                // Parse JSONL incrementally
                const newEvents = parserRef.current.parse(text);

                if (newEvents.length > 0) {
                    setEvents((prev) => [...prev, ...newEvents]);
                    setStats((prev) => ({
                        bytesRead: lastByteReadRef.current,
                        eventCount: prev.eventCount + newEvents.length,
                        lastPollTime: Date.now(),
                    }));
                }

                // Reset retry count on success
                retryCountRef.current = 0;
                setError(null);
            } else if (response.status === 416) {
                // Range Not Satisfiable - file hasn't grown yet
                // This is normal, not an error
                setError(null);
            } else if (response.status === 404) {
                // File not found - might not exist yet
                if (retryCountRef.current === 0) {
                    setError('File not found. Waiting for backtest to start...');
                }
            } else if (response.status === 200) {
                // Full content returned (server doesn't support Range)
                // Fall back to reading entire file
                console.warn('Server does not support Range headers, falling back to full read');
                const text = await response.text();
                const newEvents = parserRef.current.parse(text);

                if (newEvents.length > 0) {
                    setEvents((prev) => [...prev, ...newEvents]);
                    setStats((prev) => ({
                        bytesRead: text.length,
                        eventCount: prev.eventCount + newEvents.length,
                        lastPollTime: Date.now(),
                    }));
                }

                // Track file size for next request
                lastByteReadRef.current = text.length;
            } else {
                throw new Error(`Unexpected response status: ${response.status}`);
            }
        } catch (err) {
            // Ignore abort errors (user paused or reset)
            if (err instanceof Error && err.name === 'AbortError') {
                return;
            }

            // Handle other errors with retry logic
            retryCountRef.current++;

            if (retryCountRef.current >= MAX_RETRY_ATTEMPTS) {
                const errorMessage = err instanceof Error ? err.message : 'Unknown error';
                setError(`Failed to poll file after ${MAX_RETRY_ATTEMPTS} attempts: ${errorMessage}`);
                setIsPolling(false); // Stop polling after max retries
            } else {
                console.warn(`Poll attempt ${retryCountRef.current} failed, retrying...`, err);
            }
        }
    }, [filePath, isPolling]);

    /**
     * Set up polling interval
     */
    useEffect(() => {
        if (!isPolling) {
            // Cancel any pending request when paused
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }
            return;
        }

        // Start polling immediately
        pollFile();

        // Set up interval for subsequent polls
        const intervalId = setInterval(pollFile, POLL_INTERVAL_MS);

        // Cleanup
        return () => {
            clearInterval(intervalId);
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }
        };
    }, [isPolling, pollFile]);

    /**
     * Reset when file path changes
     */
    useEffect(() => {
        reset();
    }, [filePath, reset]);

    return {
        events,
        isPolling,
        setIsPolling,
        error,
        stats,
        reset,
    };
}
