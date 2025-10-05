import { ChartDataModel } from '@/types/chart-data';

/**
 * Loads chart data from a JSON file and validates the structure
 * @param jsonPath - Relative or absolute path to the JSON file
 * @returns Promise resolving to validated ChartDataModel
 * @throws Error if data is invalid or cannot be loaded
 */
export async function loadChartData(jsonPath: string): Promise<ChartDataModel> {
  try {
    const response = await fetch(jsonPath);

    if (!response.ok) {
      throw new Error(`Failed to load chart data: ${response.statusText}`);
    }

    const data = await response.json();
    validateChartData(data);

    return data as ChartDataModel;
  } catch (error) {
    if (error instanceof Error) {
      console.error('Error loading chart data:', error.message);
      throw error;
    }
    console.error('Error loading chart data:', error);
    throw new Error('Unknown error occurred while loading chart data');
  }
}

/**
 * Validates chart data structure against expected schema
 * Required fields: candles (non-empty array), trades (array)
 * Optional fields: indicators, walkForward
 * @param data - Data object to validate
 * @throws Error if validation fails
 */
function validateChartData(data: any): void {
  // Validate candles (required)
  if (!data.candles || !Array.isArray(data.candles)) {
    throw new Error('Invalid chart data: missing candles array');
  }

  if (data.candles.length === 0) {
    throw new Error('Invalid chart data: empty candles array');
  }

  // Validate candle structure (check first candle)
  const firstCandle = data.candles[0];
  if (!firstCandle) {
    throw new Error('Invalid chart data: candles array is empty');
  }

  const requiredCandleFields = ['time', 'open', 'high', 'low', 'close', 'volume'];
  for (const field of requiredCandleFields) {
    if (!(field in firstCandle)) {
      throw new Error(`Invalid candle data: missing required field '${field}'`);
    }
    if (typeof firstCandle[field] !== 'number') {
      throw new Error(`Invalid candle data: field '${field}' must be a number`);
    }
  }

  // Validate trades (required)
  if (!data.trades || !Array.isArray(data.trades)) {
    throw new Error('Invalid chart data: missing trades array');
  }

  // Validate trade structure (if trades exist)
  if (data.trades.length > 0) {
    const firstTrade = data.trades[0];
    const requiredTradeFields = ['time', 'price', 'volume', 'side', 'pnL'];
    for (const field of requiredTradeFields) {
      if (!(field in firstTrade)) {
        throw new Error(`Invalid trade data: missing required field '${field}'`);
      }
    }

    // Validate side is either 'buy' or 'sell'
    if (firstTrade.side !== 'buy' && firstTrade.side !== 'sell') {
      throw new Error(`Invalid trade data: side must be 'buy' or 'sell'`);
    }
  }

  // Validate optional indicators (only if present)
  if (data.indicators !== undefined) {
    if (!Array.isArray(data.indicators)) {
      throw new Error('Invalid chart data: indicators must be an array');
    }

    // Validate indicator structure (if any exist)
    if (data.indicators.length > 0) {
      const firstIndicator = data.indicators[0];
      if (!firstIndicator.name || typeof firstIndicator.name !== 'string') {
        throw new Error('Invalid indicator data: missing or invalid name');
      }
      if (!firstIndicator.values || !Array.isArray(firstIndicator.values)) {
        throw new Error('Invalid indicator data: missing values array');
      }
    }
  }

  // Validate optional walk-forward data (only if present)
  if (data.walkForward !== undefined) {
    if (typeof data.walkForward !== 'object' || data.walkForward === null) {
      throw new Error('Invalid chart data: walkForward must be an object');
    }

    const requiredWFFields = ['walkForwardEfficiency', 'consistency', 'totalWindows', 'windows'];
    for (const field of requiredWFFields) {
      if (!(field in data.walkForward)) {
        throw new Error(`Invalid walk-forward data: missing required field '${field}'`);
      }
    }

    if (!Array.isArray(data.walkForward.windows)) {
      throw new Error('Invalid walk-forward data: windows must be an array');
    }
  }
}
