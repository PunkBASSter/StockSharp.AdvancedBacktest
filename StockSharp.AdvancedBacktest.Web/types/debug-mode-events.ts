/**
 * TypeScript type definitions for Debug Mode real-time events
 * Matches C# ChartDataModels.cs with debug mode extensions
 * Used for streaming JSONL event parsing
 */

/**
 * Debug mode event wrapper for JSONL streaming
 * Each line in the JSONL file represents one DebugModeEvent
 */
export interface DebugModeEvent {
    type: 'candle' | `indicator_${string}` | 'trade' | 'state';
    data: CandleDataPoint | IndicatorDataPoint | TradeDataPoint | StateDataPoint;
}

/**
 * Candle data point with debug mode extensions
 * Matches C# CandleDataPoint class
 */
export interface CandleDataPoint {
    time: number;           // Unix timestamp (milliseconds for consistency with JavaScript Date)
    open: number;
    high: number;
    low: number;
    close: number;
    volume: number;
    sequenceNumber?: number;  // Debug mode field
    securityId?: string;      // Debug mode field
}

/**
 * Indicator data point with debug mode extensions
 * Matches C# IndicatorDataPoint class
 */
export interface IndicatorDataPoint {
    time: number;           // Unix timestamp (milliseconds)
    value: number;
    sequenceNumber?: number;  // Debug mode field

    // ZigZag-specific fields (for DeltaZigZag and similar indicators)
    /** For ZigZag indicators: true = peak, false = trough, undefined = not applicable */
    isUp?: boolean;
    /** For ZigZag indicators: true = pending (tentative), false = confirmed reversal, undefined = not applicable */
    isPending?: boolean;
    /** For pending ZigZag points: the timestamp of the bar where the extremum occurred */
    extremumTime?: number;
}

/**
 * Trade data point with debug mode extensions
 * Matches C# TradeDataPoint class
 */
export interface TradeDataPoint {
    time: number;           // Unix timestamp (milliseconds)
    price: number;
    volume: number;
    side: 'buy' | 'sell' | 'Buy' | 'Sell';  // Accept both lowercase and capitalized
    pnL: number;
    sequenceNumber?: number;  // Debug mode field
    orderId?: number;         // Debug mode field (C# uses long)
}

/**
 * State data point for strategy state tracking
 * Matches C# StateDataPoint class
 */
export interface StateDataPoint {
    time: number;           // Unix timestamp (milliseconds)
    position: number;
    pnL: number;
    unrealizedPnL: number;
    processState: string;
    sequenceNumber?: number;  // Debug mode field
}

/**
 * Type guard to validate DebugModeEvent at runtime
 */
export function isDebugModeEvent(data: unknown): data is DebugModeEvent {
    if (!data || typeof data !== 'object') return false;

    const event = data as DebugModeEvent;
    return (
        typeof event.type === 'string' &&
        (event.type === 'candle' ||
            event.type.startsWith('indicator_') ||
            event.type === 'trade' ||
            event.type === 'state') &&
        event.data !== undefined &&
        event.data !== null
    );
}

/**
 * Type guard to validate CandleDataPoint at runtime
 */
export function isCandleDataPoint(data: unknown): data is CandleDataPoint {
    if (!data || typeof data !== 'object') return false;

    const candle = data as CandleDataPoint;
    return (
        typeof candle.time === 'number' &&
        typeof candle.open === 'number' &&
        typeof candle.high === 'number' &&
        typeof candle.low === 'number' &&
        typeof candle.close === 'number' &&
        typeof candle.volume === 'number'
    );
}

/**
 * Type guard to validate IndicatorDataPoint at runtime
 */
export function isIndicatorDataPoint(data: unknown): data is IndicatorDataPoint {
    if (!data || typeof data !== 'object') return false;

    const indicator = data as IndicatorDataPoint;
    return (
        typeof indicator.time === 'number' &&
        typeof indicator.value === 'number'
    );
}

/**
 * Type guard to validate TradeDataPoint at runtime
 */
export function isTradeDataPoint(data: unknown): data is TradeDataPoint {
    if (!data || typeof data !== 'object') return false;

    const trade = data as TradeDataPoint;
    return (
        typeof trade.time === 'number' &&
        typeof trade.price === 'number' &&
        typeof trade.volume === 'number' &&
        (trade.side === 'buy' || trade.side === 'sell' || trade.side === 'Buy' || trade.side === 'Sell') &&
        typeof trade.pnL === 'number'
    );
}

/**
 * Type guard to validate StateDataPoint at runtime
 */
export function isStateDataPoint(data: unknown): data is StateDataPoint {
    if (!data || typeof data !== 'object') return false;

    const state = data as StateDataPoint;
    return (
        typeof state.time === 'number' &&
        typeof state.position === 'number' &&
        typeof state.pnL === 'number' &&
        typeof state.unrealizedPnL === 'number' &&
        typeof state.processState === 'string'
    );
}
