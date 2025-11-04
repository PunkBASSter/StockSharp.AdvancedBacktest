import { isValidTradeSide, VALIDATION_ERRORS } from '@/lib/constants/validation-constants';
import { ChartDataModel, IndicatorDataSeries } from '@/types/chart-data';

/**
 * Loads chart data from embedded window object or JSON file
 * For static exports with file:// protocol, data is embedded in window.__CHART_DATA__
 * @param jsonPath - Relative or absolute path to the JSON file (fallback)
 * @returns Promise resolving to validated ChartDataModel
 * @throws Error if data is invalid or cannot be loaded
 */
// Extend Window interface to include __CHART_DATA__
declare global {
    interface Window {
        __CHART_DATA__?: ChartDataModel;
    }
}

export async function loadChartData(jsonPath: string): Promise<ChartDataModel> {
    // First, try to load from embedded data (works with file:// protocol)
    if (typeof window !== 'undefined' && window.__CHART_DATA__) {
        try {
            const data = window.__CHART_DATA__;
            validateChartData(data);

            // Load indicator files if specified
            if (data.indicatorFiles && data.indicatorFiles.length > 0) {
                data.indicators = await loadIndicatorFiles(data.indicatorFiles);
            }

            return Promise.resolve(data as ChartDataModel);
        } catch (error) {
            // Log the specific error for debugging
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            console.error('Error loading embedded chart data:', errorMessage, error);
            // Fall through to fetch attempt
        }
    }

    // Fallback to fetching JSON (works with http/https)
    return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();

        xhr.onload = async function () {
            try {
                if (xhr.status === 200 || xhr.status === 0) {
                    const data = JSON.parse(xhr.responseText);
                    validateChartData(data);

                    // Load indicator files if specified
                    if (data.indicatorFiles && data.indicatorFiles.length > 0) {
                        data.indicators = await loadIndicatorFiles(data.indicatorFiles);
                    }

                    resolve(data as ChartDataModel);
                } else {
                    reject(new Error(`Failed to load chart data: HTTP ${xhr.status}`));
                }
            } catch (error) {
                if (error instanceof Error) {
                    console.error('Error parsing chart data:', error.message);
                    reject(error);
                } else {
                    reject(new Error('Unknown error occurred while parsing chart data'));
                }
            }
        };

        xhr.onerror = function () {
            const errorMsg = 'Failed to load chart data: Network error or file not found';
            console.error(errorMsg);
            reject(new Error(errorMsg));
        };

        xhr.open('GET', jsonPath, true);
        xhr.send();
    });
}

/**
 * Loads indicator data from separate JSON files
 * @param fileNames - Array of indicator file names relative to current directory
 * @returns Promise resolving to array of IndicatorDataSeries
 */
export async function loadIndicatorFiles(fileNames: string[]): Promise<IndicatorDataSeries[]> {
    const loadPromises = fileNames.map(fileName => {
        return new Promise<IndicatorDataSeries>((resolve, reject) => {
            const xhr = new XMLHttpRequest();

            xhr.onload = function () {
                try {
                    if (xhr.status === 200 || xhr.status === 0) {
                        const indicator = JSON.parse(xhr.responseText) as IndicatorDataSeries;
                        console.log(`Loaded indicator: ${indicator.name} (${indicator.values.length} values)`);
                        resolve(indicator);
                    } else {
                        reject(new Error(`Failed to load ${fileName}: HTTP ${xhr.status}`));
                    }
                } catch (error) {
                    reject(error);
                }
            };

            xhr.onerror = function () {
                console.warn(`Failed to load indicator file: ${fileName}`);
                // Resolve with empty indicator instead of rejecting to allow partial loading
                resolve({ name: fileName, color: '#999', values: [] });
            };

            // Load from same directory as chartData.json
            xhr.open('GET', `./${fileName}`, true);
            xhr.send();
        });
    });

    return Promise.all(loadPromises);
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
        throw new Error(VALIDATION_ERRORS.MISSING_CANDLES);
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
            throw new Error(VALIDATION_ERRORS.MISSING_CANDLE_FIELD(field));
        }
        if (typeof firstCandle[field] !== 'number') {
            throw new Error(VALIDATION_ERRORS.INVALID_CANDLE_FIELD_TYPE(field));
        }
    }

    // Validate trades (required)
    if (!chartData.trades || !Array.isArray(chartData.trades)) {
        throw new Error(VALIDATION_ERRORS.MISSING_TRADES);
    }

    // Validate trade structure (if trades exist)
    if (chartData.trades.length > 0) {
        const firstTrade = chartData.trades[0] as Record<string, unknown>;
        const requiredTradeFields = ['time', 'price', 'volume', 'side', 'pnL'];
        for (const field of requiredTradeFields) {
            if (!(field in firstTrade)) {
                throw new Error(VALIDATION_ERRORS.MISSING_TRADE_FIELD(field));
            }
        }

        // Validate side is either 'buy' or 'sell' (case-insensitive)
        const side = firstTrade.side as string;
        if (!isValidTradeSide(side)) {
            throw new Error(VALIDATION_ERRORS.INVALID_TRADE_SIDE);
        }
    }

    // Validate optional indicators (only if present)
    if (chartData.indicators !== undefined) {
        if (!Array.isArray(chartData.indicators)) {
            throw new Error(VALIDATION_ERRORS.INVALID_INDICATORS);
        }

        // Validate indicator structure (if any exist)
        if (chartData.indicators.length > 0) {
            const firstIndicator = chartData.indicators[0] as Record<string, unknown>;
            if (!firstIndicator.name || typeof firstIndicator.name !== 'string') {
                throw new Error(VALIDATION_ERRORS.INVALID_INDICATOR_NAME);
            }
            if (!firstIndicator.values || !Array.isArray(firstIndicator.values)) {
                throw new Error(VALIDATION_ERRORS.MISSING_INDICATOR_VALUES);
            }
        }
    }

    // Validate optional walk-forward data (only if present and not null)
    if (chartData.walkForward !== undefined && chartData.walkForward !== null) {
        if (typeof chartData.walkForward !== 'object') {
            throw new Error(VALIDATION_ERRORS.INVALID_WALK_FORWARD);
        }

        const walkForward = chartData.walkForward as Record<string, unknown>;
        const requiredWFFields = ['walkForwardEfficiency', 'consistency', 'totalWindows', 'windows'];
        for (const field of requiredWFFields) {
            if (!(field in walkForward)) {
                throw new Error(VALIDATION_ERRORS.MISSING_WF_FIELD(field));
            }
        }

        if (!Array.isArray(walkForward.windows)) {
            throw new Error(VALIDATION_ERRORS.INVALID_WF_WINDOWS);
        }
    }
}
