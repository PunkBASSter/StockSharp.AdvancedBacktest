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

/**
 * Walk-forward analysis results matching C# WalkForwardResult class
 * Maps to: StockSharp.AdvancedBacktest.Validation.WalkForwardResult
 */
export interface WalkForwardDataModel {
  walkForwardEfficiency: number;  // C#: WalkForwardEfficiency (calculated property)
  consistency: number;            // C#: Consistency (calculated property)
  totalWindows: number;           // C#: TotalWindows (int)
  windows: WalkForwardWindowData[];  // C#: Windows (List<WindowResult>)
}

/**
 * Individual walk-forward window result matching C# WindowResult class
 * Maps to: StockSharp.AdvancedBacktest.Validation.WindowResult
 */
export interface WalkForwardWindowData {
  windowNumber: number;              // C#: WindowNumber (int)
  trainingStart: number;             // Unix timestamp (seconds) - C#: TrainingPeriod.start
  trainingEnd: number;               // Unix timestamp (seconds) - C#: TrainingPeriod.end
  testingStart: number;              // Unix timestamp (seconds) - C#: TestingPeriod.start
  testingEnd: number;                // Unix timestamp (seconds) - C#: TestingPeriod.end
  trainingMetrics: WalkForwardMetricsData;  // C#: TrainingMetrics (PerformanceMetrics)
  testingMetrics: WalkForwardMetricsData;   // C#: TestingMetrics (PerformanceMetrics)
  performanceDegradation: number;    // C#: PerformanceDegradation (calculated property)
}

/**
 * Performance metrics matching C# PerformanceMetrics class
 * Maps to: StockSharp.AdvancedBacktest.Statistics.PerformanceMetrics
 */
export interface WalkForwardMetricsData {
  startTime: number;              // Unix timestamp (seconds) - C#: StartTime (DateTimeOffset)
  endTime: number;                // Unix timestamp (seconds) - C#: EndTime (DateTimeOffset)
  totalTrades: number;            // C#: TotalTrades (int)
  winningTrades: number;          // C#: WinningTrades (int)
  losingTrades: number;           // C#: LosingTrades (int)
  totalReturn: number;            // C#: TotalReturn (double)
  annualizedReturn: number;       // C#: AnnualizedReturn (double)
  sharpeRatio: number;            // C#: SharpeRatio (double)
  sortinoRatio: number;           // C#: SortinoRatio (double)
  maxDrawdown: number;            // C#: MaxDrawdown (double)
  winRate: number;                // C#: WinRate (double)
  profitFactor: number;           // C#: ProfitFactor (double)
  averageWin: number;             // C#: AverageWin (double)
  averageLoss: number;            // C#: AverageLoss (double)
  grossProfit: number;            // C#: GrossProfit (double)
  grossLoss: number;              // C#: GrossLoss (double)
  netProfit: number;              // C#: NetProfit (double)
  initialCapital: number;         // C#: InitialCapital (double)
  finalValue: number;             // C#: FinalValue (double)
  tradingPeriodDays: number;      // C#: TradingPeriodDays (int)
  averageTradesPerDay: number;    // C#: AverageTradesPerDay (double)
}

/**
 * Type guard to validate WalkForwardDataModel at runtime
 */
export function isWalkForwardData(data: unknown): data is WalkForwardDataModel {
  if (!data || typeof data !== "object") return false;

  const wf = data as WalkForwardDataModel;
  return (
    typeof wf.walkForwardEfficiency === "number" &&
    typeof wf.consistency === "number" &&
    typeof wf.totalWindows === "number" &&
    Array.isArray(wf.windows) &&
    wf.windows.every(isWalkForwardWindowData)
  );
}

/**
 * Type guard to validate WalkForwardWindowData at runtime
 */
export function isWalkForwardWindowData(data: unknown): data is WalkForwardWindowData {
  if (!data || typeof data !== "object") return false;

  const window = data as WalkForwardWindowData;
  return (
    typeof window.windowNumber === "number" &&
    typeof window.trainingStart === "number" &&
    typeof window.trainingEnd === "number" &&
    typeof window.testingStart === "number" &&
    typeof window.testingEnd === "number" &&
    typeof window.performanceDegradation === "number" &&
    isWalkForwardMetricsData(window.trainingMetrics) &&
    isWalkForwardMetricsData(window.testingMetrics)
  );
}

/**
 * Type guard to validate WalkForwardMetricsData at runtime
 */
export function isWalkForwardMetricsData(data: unknown): data is WalkForwardMetricsData {
  if (!data || typeof data !== "object") return false;

  const metrics = data as WalkForwardMetricsData;
  return (
    typeof metrics.startTime === "number" &&
    typeof metrics.endTime === "number" &&
    typeof metrics.totalTrades === "number" &&
    typeof metrics.winningTrades === "number" &&
    typeof metrics.losingTrades === "number" &&
    typeof metrics.totalReturn === "number" &&
    typeof metrics.annualizedReturn === "number" &&
    typeof metrics.sharpeRatio === "number" &&
    typeof metrics.sortinoRatio === "number" &&
    typeof metrics.maxDrawdown === "number" &&
    typeof metrics.winRate === "number" &&
    typeof metrics.profitFactor === "number" &&
    typeof metrics.averageWin === "number" &&
    typeof metrics.averageLoss === "number" &&
    typeof metrics.grossProfit === "number" &&
    typeof metrics.grossLoss === "number" &&
    typeof metrics.netProfit === "number" &&
    typeof metrics.initialCapital === "number" &&
    typeof metrics.finalValue === "number" &&
    typeof metrics.tradingPeriodDays === "number" &&
    typeof metrics.averageTradesPerDay === "number"
  );
}
