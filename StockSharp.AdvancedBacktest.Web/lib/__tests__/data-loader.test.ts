import { loadChartData } from '../data-loader';
import { ChartDataModel } from '@/types/chart-data';

// Mock fetch globally
global.fetch = jest.fn();

describe('loadChartData', () => {
  beforeEach(() => {
    // Clear all mocks before each test
    jest.clearAllMocks();
  });

  describe('Success cases', () => {
    it('should successfully load valid JSON with all fields', async () => {
      const mockData: ChartDataModel = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [
          { time: 1704067200, price: 100.5, volume: 10, side: 'buy', pnL: 0 },
        ],
        indicators: [
          {
            name: 'SMA',
            color: '#FF0000',
            values: [{ time: 1704067200, value: 100 }],
          },
        ],
        walkForward: {
          walkForwardEfficiency: 0.85,
          consistency: 0.9,
          totalWindows: 5,
          windows: [],
        },
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const result = await loadChartData('/test-data.json');

      expect(result).toEqual(mockData);
      expect(global.fetch).toHaveBeenCalledWith('/test-data.json');
    });

    it('should successfully load JSON with only required fields (no indicators/walk-forward)', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
          { time: 1704067260, open: 100.5, high: 101.5, low: 100, close: 101, volume: 1100 },
        ],
        trades: [
          { time: 1704067200, price: 100.5, volume: 10, side: 'buy', pnL: 0 },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const result = await loadChartData('/mock-data.json');

      expect(result).toEqual(mockData);
      expect(result.indicators).toBeUndefined();
      expect(result.walkForward).toBeUndefined();
    });

    it('should return correctly typed ChartDataModel', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const result = await loadChartData('/test.json');

      expect(Array.isArray(result.candles)).toBe(true);
      expect(Array.isArray(result.trades)).toBe(true);
      expect(result.candles[0]).toHaveProperty('time');
      expect(result.candles[0]).toHaveProperty('open');
      expect(result.candles[0]).toHaveProperty('high');
      expect(result.candles[0]).toHaveProperty('low');
      expect(result.candles[0]).toHaveProperty('close');
      expect(result.candles[0]).toHaveProperty('volume');
    });

    it('should handle partial data with indicators but no walk-forward', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
        indicators: [
          {
            name: 'EMA',
            color: '#00FF00',
            values: [{ time: 1704067200, value: 99.5 }],
          },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const result = await loadChartData('/partial-data.json');

      expect(result.indicators).toBeDefined();
      expect(result.walkForward).toBeUndefined();
    });
  });

  describe('Error cases - missing/invalid required fields', () => {
    it('should throw error for missing candles array', async () => {
      const mockData = {
        trades: [],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid.json')).rejects.toThrow(
        'Invalid chart data: missing candles array'
      );
    });

    it('should throw error for empty candles array', async () => {
      const mockData = {
        candles: [],
        trades: [],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/empty-candles.json')).rejects.toThrow(
        'Invalid chart data: empty candles array'
      );
    });

    it('should throw error for missing trades array', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/no-trades.json')).rejects.toThrow(
        'Invalid chart data: missing trades array'
      );
    });

    it('should throw error for invalid candle structure (missing fields)', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100 }, // missing high, low, close, volume
        ],
        trades: [],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-candle.json')).rejects.toThrow(
        /Invalid candle data: missing required field/
      );
    });

    it('should throw error for invalid candle field types', async () => {
      const mockData = {
        candles: [
          { time: '1704067200', open: 100, high: 101, low: 99, close: 100.5, volume: 1000 }, // time should be number
        ],
        trades: [],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-type.json')).rejects.toThrow(
        /Invalid candle data: field 'time' must be a number/
      );
    });

    it('should throw error for invalid trade side', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [
          { time: 1704067200, price: 100.5, volume: 10, side: 'invalid', pnL: 0 },
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-side.json')).rejects.toThrow(
        "Invalid trade data: side must be 'buy' or 'sell'"
      );
    });
  });

  describe('Error cases - network and HTTP errors', () => {
    it('should throw error for 404 response', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: false,
        statusText: 'Not Found',
      });

      await expect(loadChartData('/not-found.json')).rejects.toThrow(
        'Failed to load chart data: Not Found'
      );
    });

    it('should handle malformed JSON', async () => {
      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => {
          throw new SyntaxError('Unexpected token');
        },
      });

      await expect(loadChartData('/malformed.json')).rejects.toThrow();
    });

    it('should handle network errors gracefully', async () => {
      (global.fetch as jest.Mock).mockRejectedValueOnce(
        new Error('Network error')
      );

      await expect(loadChartData('/network-error.json')).rejects.toThrow(
        'Network error'
      );
    });
  });

  describe('Optional field validation', () => {
    it('should validate indicators structure only when present', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
        indicators: 'invalid', // should be array
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-indicators.json')).rejects.toThrow(
        'Invalid chart data: indicators must be an array'
      );
    });

    it('should validate indicator fields when indicators exist', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
        indicators: [
          { color: '#FF0000' }, // missing name
        ],
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-indicator-fields.json')).rejects.toThrow(
        'Invalid indicator data: missing or invalid name'
      );
    });

    it('should validate walk-forward structure only when present', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
        walkForward: 'invalid', // should be object
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/invalid-walkforward.json')).rejects.toThrow(
        'Invalid chart data: walkForward must be an object'
      );
    });

    it('should validate walk-forward fields when present', async () => {
      const mockData = {
        candles: [
          { time: 1704067200, open: 100, high: 101, low: 99, close: 100.5, volume: 1000 },
        ],
        trades: [],
        walkForward: {
          walkForwardEfficiency: 0.85,
          // missing consistency, totalWindows, windows
        },
      };

      (global.fetch as jest.Mock).mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      await expect(loadChartData('/incomplete-walkforward.json')).rejects.toThrow(
        /Invalid walk-forward data: missing required field/
      );
    });
  });
});
