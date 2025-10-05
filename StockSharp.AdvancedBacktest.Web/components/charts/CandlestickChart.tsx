'use client';
import { useEffect, useRef } from 'react';
import { createChart, ColorType, CandlestickSeries, HistogramSeries, UTCTimestamp } from 'lightweight-charts';
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

  return <div ref={chartContainerRef} className="w-full h-[600px]" />;
}
