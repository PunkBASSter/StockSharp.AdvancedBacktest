/**
 * Validation constants for trade data
 * Centralized location for trade-related validation values and error messages
 */

// ============================================================================
// Trade Side Validation
// ============================================================================

/**
 * Valid trade side values (lowercase)
 */
export const VALID_TRADE_SIDES_LOWER = ['buy', 'sell'] as const;

/**
 * Valid trade side values (capitalized)
 */
export const VALID_TRADE_SIDES_UPPER = ['Buy', 'Sell'] as const;

/**
 * All valid trade side values
 */
export const VALID_TRADE_SIDES = [
    ...VALID_TRADE_SIDES_LOWER,
    ...VALID_TRADE_SIDES_UPPER,
] as const;

/**
 * Validate if a string represents a valid trade side (case-insensitive)
 * @param side - The side value to validate
 * @returns True if the side is valid
 */
export function isValidTradeSide(side: string): boolean {
    const normalizedSide = side.toLowerCase();
    return normalizedSide === 'buy' || normalizedSide === 'sell';
}

// ============================================================================
// Validation Error Messages
// ============================================================================

export const VALIDATION_ERRORS = {
    // Chart data validation
    MISSING_CANDLES: 'Invalid chart data: missing candles array',
    MISSING_TRADES: 'Invalid chart data: missing trades array',
    INVALID_INDICATORS: 'Invalid chart data: indicators must be an array',
    INVALID_WALK_FORWARD: 'Invalid chart data: walkForward must be an object',

    // Candle validation
    MISSING_CANDLE_FIELD: (field: string) => `Invalid candle data: missing required field '${field}'`,
    INVALID_CANDLE_FIELD_TYPE: (field: string) => `Invalid candle data: field '${field}' must be a number`,

    // Trade validation
    MISSING_TRADE_FIELD: (field: string) => `Invalid trade data: missing required field '${field}'`,
    INVALID_TRADE_SIDE: `Invalid trade data: side must be 'buy' or 'sell' (case-insensitive)`,

    // Indicator validation
    INVALID_INDICATOR_NAME: 'Invalid indicator data: missing or invalid name',
    MISSING_INDICATOR_VALUES: 'Invalid indicator data: missing values array',

    // Walk-forward validation
    MISSING_WF_FIELD: (field: string) => `Invalid walk-forward data: missing required field '${field}'`,
    INVALID_WF_WINDOWS: 'Invalid walk-forward data: windows must be an array',
} as const;
