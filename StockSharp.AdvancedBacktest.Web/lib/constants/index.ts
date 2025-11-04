/**
 * Centralized constants for the application
 * Re-exports all constant modules for convenient importing
 */

// Chart-related constants
export {
    CANDLE_COLORS, DEFAULT_INDICATOR_COLOR, INDICATOR_COLORS, MARKER_POSITION,
    MARKER_SHAPE, TRADE_MARKER_COLORS, TRADE_SIDE,
    TRADE_SIDE_LABEL, formatTradeMarkerText, getIndicatorColor, getTradeMarkerConfig, getVolumeColor, isBuySide,
    isSellSide
} from './chart-constants';

// Validation-related constants
export {
    VALIDATION_ERRORS, VALID_TRADE_SIDES, VALID_TRADE_SIDES_LOWER,
    VALID_TRADE_SIDES_UPPER, isValidTradeSide
} from './validation-constants';

