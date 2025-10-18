'use client';
import { ChartDataModel } from '@/types/chart-data';
import { createChart, ColorType, UTCTimestamp } from 'lightweight-charts';
import { useEffect, useRef } from 'react';

interface Props {
    data: ChartDataModel;
}

export default function CandlestickChart({ data }: Props) {
    const chartContainerRef = useRef<HTMLDivElement>(null);

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

        // Add candlestick series using v4 API
        const candlestickSeries = chart.addCandlestickSeries({
            upColor: '#26a69a',
            downColor: '#ef5350',
            borderVisible: false,
            wickUpColor: '#26a69a',
            wickDownColor: '#ef5350',
        });

        // Set candlestick data
        const candleData = data.candles.map(candle => ({
            time: candle.time as UTCTimestamp,
            open: candle.open,
            high: candle.high,
            low: candle.low,
            close: candle.close,
        }));

        candlestickSeries.setData(candleData);

        // Add trade markers using v4 setMarkers API
        if (data.trades && data.trades.length > 0) {
            const markers = data.trades.map(trade => {
                const isBuy = trade.side === 'buy';
                return {
                    time: trade.time as UTCTimestamp,
                    position: (isBuy ? 'belowBar' : 'aboveBar') as 'belowBar' | 'aboveBar',
                    color: isBuy ? '#2196F3' : '#F44336',
                    shape: (isBuy ? 'arrowUp' : 'arrowDown') as 'arrowUp' | 'arrowDown',
                    text: `${isBuy ? 'BUY' : 'SELL'} @ ${trade.price}`,
                };
            });
            candlestickSeries.setMarkers(markers);
        }

        // Add volume series if volume data exists using v4 API
        if (data.candles.length > 0 && data.candles[0].volume !== undefined) {
            const volumeSeries = chart.addHistogramSeries({
                color: '#26a69a',
                priceFormat: {
                    type: 'volume',
                },
                priceScaleId: '',
            });

            // Set volume data with color based on candle direction
            const volumeData = data.candles.map(candle => ({
                time: candle.time as UTCTimestamp,
                value: candle.volume,
                color: candle.close >= candle.open ? '#26a69a80' : '#ef535080', // Semi-transparent
            }));

            volumeSeries.setData(volumeData);

            // Scale volume series to 20% of chart height
            volumeSeries.priceScale().applyOptions({
                scaleMargins: {
                    top: 0.8,
                    bottom: 0,
                },
            });
        }

        // Add indicator line series if indicators exist using v4 API
        if (data.indicators && data.indicators.length > 0) {
            data.indicators.forEach(indicator => {
                const lineSeries = chart.addLineSeries({
                    color: indicator.color || '#2196F3',
                    lineWidth: 2,
                    title: indicator.name,
                    priceLineVisible: false,
                    crosshairMarkerVisible: true,
                });

                const indicatorData = indicator.values.map(point => ({
                    time: point.time as UTCTimestamp,
                    value: point.value,
                }));

                lineSeries.setData(indicatorData);
            });
        }

        // Set visible range to show first 6 months or where trades are
        // This makes trade markers more visible than fitContent() which zooms too far out
        if (data.trades && data.trades.length > 0) {
            // Find the time range of trades
            const tradeTimes = data.trades.map(t => t.time);
            const minTradeTime = Math.min(...tradeTimes);
            const maxTradeTime = Math.max(...tradeTimes);

            // Add some padding (30 days before first trade, 30 days after last trade)
            const padding = 30 * 24 * 60 * 60; // 30 days in seconds
            chart.timeScale().setVisibleRange({
                from: (minTradeTime - padding) as UTCTimestamp,
                to: (maxTradeTime + padding) as UTCTimestamp,
            });
        } else {
            // No trades, show first 6 months of data
            if (candleData.length > 0) {
                const firstTime = candleData[0].time as number;
                const sixMonths = 180 * 24 * 60 * 60; // 180 days in seconds
                chart.timeScale().setVisibleRange({
                    from: firstTime as UTCTimestamp,
                    to: (firstTime + sixMonths) as UTCTimestamp,
                });
            }
        }

        // Handle window resize
        const handleResize = () => {
            if (chartContainerRef.current) {
                chart.applyOptions({
                    width: chartContainerRef.current.clientWidth,
                });
            }
        };

        window.addEventListener('resize', handleResize);

        // Cleanup
        return () => {
            window.removeEventListener('resize', handleResize);
            chart.remove();
        };
    }, [data]);

    return (
        <div className="relative w-full">
            <div ref={chartContainerRef} className="w-full h-[400px] md:h-[600px]" />

            {/* Trade Markers Legend */}
            <div className="absolute top-4 right-4 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-lg shadow-md p-3 space-y-2">
                <div className="flex items-center gap-2">
                    <div className="marker marker-buy" />
                    <span className="text-sm font-medium text-gray-700">Buy Orders</span>
                </div>
                <div className="flex items-center gap-2">
                    <div className="marker marker-sell" />
                    <span className="text-sm font-medium text-gray-700">Sell Orders</span>
                </div>
            </div>

            {/* Indicator Legend */}
            {data.indicators && data.indicators.length > 0 && (
                <div className="absolute top-4 left-4 bg-white/90 backdrop-blur-sm border border-gray-200 rounded-lg shadow-md p-3">
                    <h3 className="text-sm font-semibold text-gray-800 mb-2">Indicators</h3>
                    <div className="space-y-2">
                        {data.indicators.map((indicator) => (
                            <div key={indicator.name} className="flex items-center gap-2">
                                <div
                                    className="w-4 h-0.5 rounded-full"
                                    style={{ backgroundColor: indicator.color }}
                                />
                                <span className="text-sm font-medium text-gray-700">{indicator.name}</span>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}
