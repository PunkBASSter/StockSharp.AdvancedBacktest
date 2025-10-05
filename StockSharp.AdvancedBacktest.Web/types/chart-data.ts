/**
 * TypeScript type definitions matching C# ChartDataModels.cs
 * Used for type-safe JSON data loading in the visualization layer
 */

export interface ChartDataModel {
  candles: CandleDataPoint[];
  indicators?: IndicatorDataSeries[];
  trades: TradeDataPoint[];
  walkForward?: WalkForwardDataModel;
}

export interface CandleDataPoint {
  time: number;           // Unix timestamp (seconds)
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
}

export interface IndicatorDataSeries {
  name: string;
  color: string;
  values: IndicatorDataPoint[];
}

export interface IndicatorDataPoint {
  time: number;           // Unix timestamp (seconds)
  value: number;
}

export interface TradeDataPoint {
  time: number;           // Unix timestamp (seconds)
  price: number;
  volume: number;
  side: 'buy' | 'sell';
  pnL: number;
}

export interface WalkForwardDataModel {
  walkForwardEfficiency: number;
  consistency: number;
  totalWindows: number;
  windows: WalkForwardWindowData[];
}

export interface WalkForwardWindowData {
  windowNumber: number;
  trainingStart: number;  // Unix timestamp (seconds)
  trainingEnd: number;    // Unix timestamp (seconds)
  testingStart: number;   // Unix timestamp (seconds)
  testingEnd: number;     // Unix timestamp (seconds)
  trainingMetrics: WalkForwardMetricsData;
  testingMetrics: WalkForwardMetricsData;
  performanceDegradation: number;
}

export interface WalkForwardMetricsData {
  totalReturn: number;
  sharpeRatio: number;
  sortinoRatio: number;
  maxDrawdown: number;
  winRate: number;
  profitFactor: number;
  totalTrades: number;
}
