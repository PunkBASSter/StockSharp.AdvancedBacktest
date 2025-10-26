'use client';

/**
 * Real-time chart component for debug mode visualization
 * Updates incrementally as events arrive from JSONL stream
 */

import {
    CandleDataPoint,
    DebugModeEvent,
    IndicatorDataPoint,
    TradeDataPoint,
} from '@/types/debug-mode-events';
import {
    CandlestickData,
    ColorType,
    createChart,
    IChartApi,
    ISeriesApi,
    LineData,
    SeriesMarker,
    Time,
    UTCTimestamp,
} from 'lightweight-charts';
import { useCallback, useEffect, useRef, useState } from 'react';

interface Props {
    events: DebugModeEvent[];
}

// Maximum candles to keep in memory for performance
const MAX_CANDLE_HISTORY = 10000;

// Indicator colors (cycle through these for multiple indicators)
const INDICATOR_COLORS = [
    '#2196F3', // Blue
    '#FF9800', // Orange
    '#4CAF50', // Green
    '#9C27B0', // Purple
    '#F44336', // Red
    '#00BCD4', // Cyan
    '#FFEB3B', // Yellow
    '#795548', // Brown
];

/**
 * Convert Unix timestamp (milliseconds) to UTCTimestamp (seconds)
 */
function toUTCTimestamp(timeMs: number): UTCTimestamp {
    return Math.floor(timeMs / 1000) as UTCTimestamp;
}

export default function DebugModeChart({ events }: Props) {
    const chartContainerRef = useRef<HTMLDivElement>(null);
    const chartRef = useRef<IChartApi | null>(null);
    const candleSeriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null);
    const volumeSeriesRef = useRef<ISeriesApi<'Histogram'> | null>(null);
    const indicatorSeriesRef = useRef<Map<string, ISeriesApi<'Line'>>>(new Map());
    const indicatorColorIndexRef = useRef<number>(0);

    // Track indicator names for legend rendering
    const [indicatorNames, setIndicatorNames] = useState<string[]>([]);

    // Pending updates for batching (requestAnimationFrame)
    const pendingCandlesRef = useRef<Map<number, CandleDataPoint>>(new Map());
    const pendingIndicatorsRef = useRef<Map<string, IndicatorDataPoint[]>>(new Map());
    const pendingMarkersRef = useRef<TradeDataPoint[]>([]);
    const rafIdRef = useRef<number | null>(null);

    // Accumulated indicator history (persist across updates)
    const indicatorHistoryRef = useRef<Map<string, Map<number, IndicatorDataPoint>>>(new Map());

    /**
     * Initialize chart on mount
     */
    useEffect(() => {
        if (!chartContainerRef.current) return;

        // Create chart instance
        const chart = createChart(chartContainerRef.current, {
            width: chartContainerRef.current.clientWidth,
            height: 600,
            layout: {
                textColor: '#333',
                background: { type: ColorType.Solid, color: '#ffffff' },
            },
            grid: {
                vertLines: { color: '#e1e1e1' },
                horzLines: { color: '#e1e1e1' },
            },
            rightPriceScale: {
                borderColor: '#D1D4DC',
            },
            timeScale: {
                borderColor: '#D1D4DC',
                timeVisible: true,
                secondsVisible: false,
            },
        });

        chartRef.current = chart;

        // Add candlestick series
        const candleSeries = chart.addCandlestickSeries({
            upColor: '#26a69a',
            downColor: '#ef5350',
            borderVisible: false,
            wickUpColor: '#26a69a',
            wickDownColor: '#ef5350',
        });

        candleSeriesRef.current = candleSeries;

        // Add volume series
        const volumeSeries = chart.addHistogramSeries({
            color: '#26a69a',
            priceFormat: {
                type: 'volume',
            },
            priceScaleId: '',
        });

        volumeSeriesRef.current = volumeSeries;

        // Scale volume series to 20% of chart height
        volumeSeries.priceScale().applyOptions({
            scaleMargins: {
                top: 0.8,
                bottom: 0,
            },
        });

        // Handle window resize
        const handleResize = () => {
            if (chartContainerRef.current && chartRef.current) {
                chartRef.current.applyOptions({
                    width: chartContainerRef.current.clientWidth,
                });
            }
        };

        window.addEventListener('resize', handleResize);

        // Capture refs for cleanup
        const capturedIndicatorSeriesRef = indicatorSeriesRef;

        // Cleanup
        return () => {
            window.removeEventListener('resize', handleResize);
            if (rafIdRef.current !== null) {
                cancelAnimationFrame(rafIdRef.current);
            }
            chart.remove();

            // Clear all series refs since chart is being destroyed
            candleSeriesRef.current = null;
            volumeSeriesRef.current = null;
            capturedIndicatorSeriesRef.current.clear();
            chartRef.current = null;
        };
    }, []);

    /**
     * Flush pending updates to chart (batched for performance)
     * Note: Using useRef to avoid stale closures
     */
    const flushUpdatesRef = useRef<() => void>(() => { });

    flushUpdatesRef.current = () => {
        if (!candleSeriesRef.current || !volumeSeriesRef.current) {
            rafIdRef.current = null;
            return;
        }

        // Update candles
        if (pendingCandlesRef.current.size > 0) {
            const candleArray = Array.from(pendingCandlesRef.current.values());

            // Sort by time
            candleArray.sort((a, b) => a.time - b.time);

            // Limit history to prevent memory issues
            const limitedCandles = candleArray.slice(-MAX_CANDLE_HISTORY);

            // Convert to chart format
            const candleData: CandlestickData[] = limitedCandles.map((candle) => {
                const timestamp = toUTCTimestamp(candle.time);
                return {
                    time: timestamp,
                    open: Number(candle.open),
                    high: Number(candle.high),
                    low: Number(candle.low),
                    close: Number(candle.close),
                };
            });

            const volumeData = limitedCandles.map((candle) => {
                const timestamp = toUTCTimestamp(candle.time);
                return {
                    time: timestamp,
                    value: Number(candle.volume),
                    color: candle.close >= candle.open ? '#26a69a80' : '#ef535080',
                };
            });

            candleSeriesRef.current.setData(candleData);
            volumeSeriesRef.current.setData(volumeData);

            // Clear pending candles
            pendingCandlesRef.current.clear();
        }

        // Update indicators
        if (pendingIndicatorsRef.current.size > 0) {
            for (const [indicatorName, points] of pendingIndicatorsRef.current.entries()) {
                // Get or create indicator history map
                let historyMap = indicatorHistoryRef.current.get(indicatorName);
                if (!historyMap) {
                    historyMap = new Map<number, IndicatorDataPoint>();
                    indicatorHistoryRef.current.set(indicatorName, historyMap);
                }

                // Add new points to history (by timestamp to avoid duplicates)
                for (const point of points) {
                    historyMap.set(point.time, point);
                }

                let series = indicatorSeriesRef.current.get(indicatorName);

                // Create series if it doesn't exist
                if (!series && chartRef.current) {
                    const colorIndex = indicatorColorIndexRef.current % INDICATOR_COLORS.length;
                    const color = INDICATOR_COLORS[colorIndex];
                    indicatorColorIndexRef.current++;

                    series = chartRef.current.addLineSeries({
                        color,
                        lineWidth: 3,
                        title: indicatorName,
                        priceLineVisible: false,
                        crosshairMarkerVisible: true,
                        lastValueVisible: true,
                        // Don't specify priceScaleId - use default (right) which is same as candles
                    });

                    indicatorSeriesRef.current.set(indicatorName, series);

                    // Update indicator names state to trigger legend re-render
                    setIndicatorNames(Array.from(indicatorSeriesRef.current.keys()));
                }

                if (series) {
                    // Convert accumulated history to chart format
                    const allPoints = Array.from(historyMap.values());
                    allPoints.sort((a, b) => a.time - b.time);

                    const lineData: LineData[] = allPoints.map((point) => ({
                        time: toUTCTimestamp(point.time),
                        value: point.value,
                    }));

                    series.setData(lineData);
                }
            }

            // Clear pending indicators
            pendingIndicatorsRef.current.clear();

            // Fit content after updating indicators to ensure they're visible
            if (chartRef.current) {
                chartRef.current.timeScale().fitContent();
            }
        }

        // Update trade markers
        if (pendingMarkersRef.current.length > 0 && candleSeriesRef.current) {
            const markers: SeriesMarker<Time>[] = pendingMarkersRef.current.map((trade) => {
                const isBuy = trade.side === 'buy';
                return {
                    time: toUTCTimestamp(trade.time),
                    position: (isBuy ? 'belowBar' : 'aboveBar') as 'belowBar' | 'aboveBar',
                    color: isBuy ? '#2196F3' : '#F44336',
                    shape: (isBuy ? 'arrowUp' : 'arrowDown') as 'arrowUp' | 'arrowDown',
                    text: `${isBuy ? 'BUY' : 'SELL'} @ ${trade.price.toFixed(2)}`,
                };
            });

            candleSeriesRef.current.setMarkers(markers);

            // Clear pending markers
            pendingMarkersRef.current = [];
        }

        // Auto-scroll to show latest data
        if (chartRef.current) {
            chartRef.current.timeScale().scrollToRealTime();
        }

        rafIdRef.current = null;
    };

    /**
     * Schedule chart update
     * Note: We don't use RAF because events already come in 500ms batches from polling
     */
    const scheduleUpdate = useCallback(() => {
        // Call flush immediately since polling already batches events
        flushUpdatesRef.current?.();
    }, []);

    /**
     * Process incoming events
     */
    useEffect(() => {
        if (events.length === 0) return;

        // Accumulate candles by time (merge duplicates)
        const candleMap = new Map<number, CandleDataPoint>();
        const indicatorMap = new Map<string, IndicatorDataPoint[]>();
        const trades: TradeDataPoint[] = [];

        for (const event of events) {
            switch (event.type) {
                case 'candle': {
                    const candle = event.data as CandleDataPoint;
                    // Validate candle data
                    if (candle && typeof candle.time === 'number' &&
                        typeof candle.open === 'number' &&
                        typeof candle.high === 'number' &&
                        typeof candle.low === 'number' &&
                        typeof candle.close === 'number' &&
                        typeof candle.volume === 'number') {
                        candleMap.set(candle.time, candle);
                    }
                    break;
                }

                case 'trade': {
                    const trade = event.data as TradeDataPoint;
                    trades.push(trade);
                    break;
                }

                default:
                    // indicator_* types
                    if (event.type.startsWith('indicator_')) {
                        const indicator = event.data as IndicatorDataPoint;
                        const indicatorName = event.type.replace('indicator_', '');

                        if (!indicatorMap.has(indicatorName)) {
                            indicatorMap.set(indicatorName, []);
                        }
                        indicatorMap.get(indicatorName)!.push(indicator);
                    }
                    break;
            }
        }

        // Update pending state
        pendingCandlesRef.current = candleMap;
        pendingIndicatorsRef.current = indicatorMap;
        pendingMarkersRef.current = trades;

        // Schedule update
        scheduleUpdate();
    }, [events, scheduleUpdate]);

    return (
        <div className="relative w-full">
            <div ref={chartContainerRef} className="w-full h-[400px] md:h-[600px]" />

            {/* Trade Markers Legend */}
            <div className="absolute top-4 right-4 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-lg shadow-md p-3 space-y-2">
                <div className="flex items-center gap-2">
                    <div className="w-0 h-0 border-l-[6px] border-l-transparent border-r-[6px] border-r-transparent border-b-[8px] border-b-blue-500" />
                    <span className="text-sm font-medium text-gray-700">Buy Orders</span>
                </div>
                <div className="flex items-center gap-2">
                    <div className="w-0 h-0 border-l-[6px] border-l-transparent border-r-[6px] border-r-transparent border-t-[8px] border-t-red-500" />
                    <span className="text-sm font-medium text-gray-700">Sell Orders</span>
                </div>
            </div>

            {/* Indicator Legend */}
            {indicatorNames.length > 0 && (
                <div className="absolute top-4 left-4 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-lg shadow-md p-3">
                    <h3 className="text-sm font-semibold text-gray-800 mb-2">Indicators</h3>
                    <div className="space-y-2">
                        {indicatorNames.map((name, index) => (
                            <div key={name} className="flex items-center gap-2">
                                <div
                                    className="w-4 h-0.5 rounded-full"
                                    style={{
                                        backgroundColor: INDICATOR_COLORS[index % INDICATOR_COLORS.length],
                                    }}
                                />
                                <span className="text-sm font-medium text-gray-700">{name}</span>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}
