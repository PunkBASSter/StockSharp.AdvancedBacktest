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
function validateChartData(data: unknown): void {
  // Type guard: ensure data is an object
  if (typeof data !== 'object' || data === null) {
    throw new Error('Invalid chart data: data must be an object');
  }

  // Cast to Record type for property access
  const chartData = data as Record<string, unknown>;

  // Validate candles (required)
  if (!chartData.candles || !Array.isArray(chartData.candles)) {
    throw new Error('Invalid chart data: missing candles array');
  }

  if (chartData.candles.length === 0) {
    throw new Error('Invalid chart data: empty candles array');
  }

  // Validate candle structure (check first candle)
  const firstCandle = chartData.candles[0] as Record<string, unknown>;
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
  if (!chartData.trades || !Array.isArray(chartData.trades)) {
    throw new Error('Invalid chart data: missing trades array');
  }

  // Validate trade structure (if trades exist)
  if (chartData.trades.length > 0) {
    const firstTrade = chartData.trades[0] as Record<string, unknown>;
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
  if (chartData.indicators !== undefined) {
    if (!Array.isArray(chartData.indicators)) {
      throw new Error('Invalid chart data: indicators must be an array');
    }

    // Validate indicator structure (if any exist)
    if (chartData.indicators.length > 0) {
      const firstIndicator = chartData.indicators[0] as Record<string, unknown>;
      if (!firstIndicator.name || typeof firstIndicator.name !== 'string') {
        throw new Error('Invalid indicator data: missing or invalid name');
      }
      if (!firstIndicator.values || !Array.isArray(firstIndicator.values)) {
        throw new Error('Invalid indicator data: missing values array');
      }
    }
  }

  // Validate optional walk-forward data (only if present)
  if (chartData.walkForward !== undefined) {
    if (typeof chartData.walkForward !== 'object' || chartData.walkForward === null) {
      throw new Error('Invalid chart data: walkForward must be an object');
    }

    const walkForward = chartData.walkForward as Record<string, unknown>;
    const requiredWFFields = ['walkForwardEfficiency', 'consistency', 'totalWindows', 'windows'];
    for (const field of requiredWFFields) {
      if (!(field in walkForward)) {
        throw new Error(`Invalid walk-forward data: missing required field '${field}'`);
      }
    }

    if (!Array.isArray(walkForward.windows)) {
      throw new Error('Invalid walk-forward data: windows must be an array');
    }
  }
}
