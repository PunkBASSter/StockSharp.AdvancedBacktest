'use client';
import { useEffect, useRef } from 'react';
import { createChart, ColorType, CandlestickSeries, HistogramSeries, LineSeries, UTCTimestamp } from 'lightweight-charts';
import { ChartDataModel } from '@/types/chart-data';

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

    // Add candlestick series
    const candlestickSeries = chart.addSeries(CandlestickSeries, {
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

    // Add trade markers to candlestick series
    if (data.trades && data.trades.length > 0) {
      const markers = data.trades.map(trade => {
        const isBuy = trade.side === 'buy';
        return {
          time: trade.time as UTCTimestamp,
          position: isBuy ? ('belowBar' as const) : ('aboveBar' as const),
          color: isBuy ? '#2196F3' : '#F44336',
          shape: isBuy ? ('square' as const) : ('circle' as const),
          text: '',
        };
      });
      // TypeScript types don't expose setMarkers on candlestick series, but it exists at runtime
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (candlestickSeries as any).setMarkers(markers);
    }

    // Add volume series if volume data exists
    if (data.candles.length > 0 && data.candles[0].volume !== undefined) {
      const volumeSeries = chart.addSeries(HistogramSeries, {
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

    // Add indicator line series if indicators exist
    if (data.indicators && data.indicators.length > 0) {
      data.indicators.forEach(indicator => {
        const lineSeries = chart.addSeries(LineSeries, {
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

    // Fit content to show all data
    chart.timeScale().fitContent();

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
      <div ref={chartContainerRef} className="w-full h-[600px]" />

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
