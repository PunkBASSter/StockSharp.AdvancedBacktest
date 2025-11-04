/**
 * Chart-related constants for trading visualization
 * Centralized location for colors, labels, and trade-related string values
 */

// ============================================================================
// Trade Side Constants
// ============================================================================

/**
 * Trade side string values (case variations)
 * The backend C# may send capitalized values while frontend uses lowercase
 */
export const TRADE_SIDE = {
    BUY_LOWER: 'buy' as const,
    SELL_LOWER: 'sell' as const,
    BUY_UPPER: 'Buy' as const,
    SELL_UPPER: 'Sell' as const,
} as const;

/**
 * Trade side display labels
 */
export const TRADE_SIDE_LABEL = {
    BUY: 'BUY' as const,
    SELL: 'SELL' as const,
} as const;

/**
 * Check if a value represents a buy side (case-insensitive)
 */
export function isBuySide(side: string): boolean {
    return side.toLowerCase() === TRADE_SIDE.BUY_LOWER;
}

/**
 * Check if a value represents a sell side (case-insensitive)
 */
export function isSellSide(side: string): boolean {
    return side.toLowerCase() === TRADE_SIDE.SELL_LOWER;
}

// ============================================================================
// Chart Colors
// ============================================================================

/**
 * Color palette for candlestick charts
 */
export const CANDLE_COLORS = {
    /** Bullish candle color (green) */
    UP: '#26a69a',
    /** Bearish candle color (red) */
    DOWN: '#ef5350',
    /** Bullish candle color with transparency */
    UP_TRANSPARENT: '#26a69a80',
    /** Bearish candle color with transparency */
    DOWN_TRANSPARENT: '#ef535080',
} as const;

/**
 * Color palette for trade markers
 */
export const TRADE_MARKER_COLORS = {
    /** Buy trade marker color (blue) */
    BUY: '#2196F3',
    /** Sell trade marker color (red) */
    SELL: '#F44336',
} as const;

/**
 * Color palette for indicators
 * Used to cycle through multiple indicators on the same chart
 */
export const INDICATOR_COLORS = [
    '#2196F3', // Blue
    '#FF9800', // Orange
    '#4CAF50', // Green
    '#9C27B0', // Purple
    '#F44336', // Red
    '#00BCD4', // Cyan
    '#FFEB3B', // Yellow
    '#795548', // Brown
] as const;

/**
 * Default indicator color (fallback)
 */
export const DEFAULT_INDICATOR_COLOR = INDICATOR_COLORS[0];

// ============================================================================
// Chart Marker Configuration
// ============================================================================

/**
 * Marker positions relative to candlestick bars
 */
export const MARKER_POSITION = {
    ABOVE_BAR: 'aboveBar' as const,
    BELOW_BAR: 'belowBar' as const,
} as const;

/**
 * Marker shapes for lightweight-charts
 */
export const MARKER_SHAPE = {
    ARROW_UP: 'arrowUp' as const,
    ARROW_DOWN: 'arrowDown' as const,
    CIRCLE: 'circle' as const,
    SQUARE: 'square' as const,
} as const;

// ============================================================================
// Price Line Configuration
// ============================================================================

/**
 * Line styles for price lines (corresponds to LineStyle enum in lightweight-charts)
 */
export const PRICE_LINE_STYLE = {
    SOLID: 0,
    DOTTED: 1,
    DASHED: 2,
    LARGE_DASHED: 3,
    SPARSE_DOTTED: 4,
} as const;

/**
 * Price line configuration for trade markers
 */
export const TRADE_PRICE_LINE = {
    /** Line width in pixels */
    LINE_WIDTH: 1,
    /** Line style (dashed for subtle appearance) */
    LINE_STYLE: PRICE_LINE_STYLE.DASHED,
    /** Axis label visibility */
    AXIS_LABEL_VISIBLE: false,
} as const;

// ============================================================================
// Chart Viewport Configuration
// ============================================================================

/**
 * Number of visible bars in debug mode chart
 * Controls the default viewport width to show the last 48 candles
 */
export const DEBUG_CHART_VISIBLE_BARS = 48;

/**
 * Right offset for debug mode chart (in bars)
 * Creates empty space between the last candle and the right border
 * Represents 25% of the 48-bar viewport for better UX
 */
export const DEBUG_CHART_RIGHT_OFFSET_BARS = 12;

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get marker configuration for a trade based on side
 * @param side - Trade side ('buy', 'sell', 'Buy', or 'Sell')
 * @returns Object with position, color, and shape for the marker
 */
export function getTradeMarkerConfig(side: string) {
    const isBuy = isBuySide(side);
    return {
        position: isBuy ? MARKER_POSITION.BELOW_BAR : MARKER_POSITION.ABOVE_BAR,
        color: isBuy ? TRADE_MARKER_COLORS.BUY : TRADE_MARKER_COLORS.SELL,
        shape: isBuy ? MARKER_SHAPE.ARROW_UP : MARKER_SHAPE.ARROW_DOWN,
        label: isBuy ? TRADE_SIDE_LABEL.BUY : TRADE_SIDE_LABEL.SELL,
    };
}

/**
 * Format trade marker text with price
 * @param side - Trade side
 * @param price - Trade price
 * @param decimals - Number of decimal places (default: 2)
 * @returns Formatted marker text (e.g., "BUY @ 7344.96")
 */
export function formatTradeMarkerText(side: string, price: number, decimals: number = 2): string {
    const label = isBuySide(side) ? TRADE_SIDE_LABEL.BUY : TRADE_SIDE_LABEL.SELL;
    return `${label} @ ${price.toFixed(decimals)}`;
}

/**
 * Get candle volume color based on direction
 * @param close - Candle close price
 * @param open - Candle open price
 * @returns Color with transparency for volume histogram
 */
export function getVolumeColor(close: number, open: number): string {
    return close >= open ? CANDLE_COLORS.UP_TRANSPARENT : CANDLE_COLORS.DOWN_TRANSPARENT;
}

/**
 * Get indicator color by index (cycles through available colors)
 * @param index - Indicator index
 * @returns Color hex string
 */
export function getIndicatorColor(index: number): string {
    return INDICATOR_COLORS[index % INDICATOR_COLORS.length];
}
